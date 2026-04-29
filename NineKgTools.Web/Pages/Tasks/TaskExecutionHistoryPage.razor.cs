using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Medias;
using NineKgTools.Components.Tasks;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Tasks;

public partial class TaskExecutionHistoryPage : ComponentBase, IDisposable
{
    [Inject] private UnifiedTaskService TaskService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private Config Config { get; set; } = null!;
    [Inject] private FilesService FilesService { get; set; } = null!;
    [Inject] private MediaService MediaService { get; set; } = null!;

    private List<TaskExecutionInfo> _allHistory = new();
    private List<TaskExecutionInfo> _filteredHistory = new();

    // 筛选条件
    private string _searchText = "";
    private bool? _statusFilter;
    private TaskType? _searchTaskType;

    private System.Threading.Timer? _refreshTimer;
    private bool _isDisposed;

    protected override async Task OnInitializedAsync()
    {
        await LoadHistoryAsync();

        // 定时刷新（每10秒一次）
        _refreshTimer = new System.Threading.Timer(OnTimerCallback, null,
            TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    private async void OnTimerCallback(object? state)
    {
        if (_isDisposed) return;

        try
        {
            await InvokeAsync(async () =>
            {
                await LoadHistoryAsync();
                StateHasChanged();
            });
        }
        catch (ObjectDisposedException)
        {
            // 组件已释放，忽略
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "定时刷新执行历史数据时出错");
        }
    }

    private Task LoadHistoryAsync()
    {
        try
        {
            _allHistory = TaskService.GetExecutionHistory().ToList();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载执行历史失败");
            Snackbar.Add("加载执行历史失败", Severity.Error);
        }

        return Task.CompletedTask;
    }

    private void ApplyFilters()
    {
        var query = _allHistory.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_searchText))
            query = query.Where(x => x.TaskName.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        if (_statusFilter.HasValue)
            query = query.Where(x => x.Success == _statusFilter.Value);

        if (_searchTaskType.HasValue)
            query = query.Where(x => x.TaskType == _searchTaskType.Value);

        _filteredHistory = query.OrderByDescending(x => x.StartTime).ToList();
    }

    private void ResetFilters()
    {
        _searchText = "";
        _statusFilter = null;
        _searchTaskType = null;
        ApplyFilters();
    }

    private async Task RefreshAsync()
    {
        await LoadHistoryAsync();
        Snackbar.Add("刷新成功", Severity.Success);
    }

    private async Task ShowHistoryDetailsAsync(TaskExecutionInfo info)
    {
        var parameters = new DialogParameters { ["ExecutionInfo"] = info };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        await DialogService.ShowAsync<TaskHistoryDetailsDialog>("任务执行详情", parameters, options);
    }

    /// <summary>
    /// 判断是否可以重新识别
    /// </summary>
    private bool CanRetryIdentification(TaskExecutionInfo info)
    {
        // 只有失败的单文件识别任务且有源路径才能重新识别
        return !info.Success &&
               info.TaskType == TaskType.SingleSourceIdentification &&
               !string.IsNullOrEmpty(info.SourcePath);
    }

    /// <summary>
    /// 处理重新手动识别
    /// </summary>
    private async Task HandleRetryIdentificationAsync(TaskExecutionInfo info)
    {
        if (string.IsNullOrEmpty(info.SourcePath))
        {
            Snackbar.Add("无法获取源路径，请从媒体源页面重新识别", Severity.Error);
            return;
        }

        // 检查路径是否存在
        if (!File.Exists(info.SourcePath) && !Directory.Exists(info.SourcePath))
        {
            Log.Warning("源路径不存在: {Path}", info.SourcePath);
            Snackbar.Add("源路径不存在，可能已被移动或删除", Severity.Error);
            return;
        }

        var initOptions = Config.Identification.ToIdentificationOptions();
        initOptions.AutoAddToDatabase = false;

        // 1. 打开IdentificationOptionsDialog让用户配置识别选项
        var parameters = new DialogParameters
        {
            { "SourcePath", info.SourcePath },
            { "InitialOptions", initOptions }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<IdentificationOptionsDialog>("识别选项配置", parameters, options);
        var result = await dialog.Result;

        // 用户取消配置
        if (result == null || result.Canceled)
            return;

        var identificationOptions = result.Data as Core.Models.Identification.IdentificationOptions;
        if (identificationOptions == null)
        {
            Snackbar.Add("获取识别选项失败", Severity.Error);
            return;
        }

        // 创建取消令牌源
        using var cancellationTokenSource = new System.Threading.CancellationTokenSource();

        // 创建进度报告器
        var progressReporter = new DialogProgressReporter();

        // 2. 显示识别进度对话框（带进度条和取消按钮）
        var loadingParameters = new DialogParameters
        {
            { "OnCancel", EventCallback.Factory.Create(this, () =>
                {
                    cancellationTokenSource.Cancel();
                    Snackbar.Add("已请求取消识别", Severity.Info);
                })
            }
        };

        var loadingOptions = new DialogOptions
        {
            NoHeader = true,
            CloseButton = false,
            CloseOnEscapeKey = false,
            Position = DialogPosition.Center
        };

        var loadingDialogRef = await DialogService.ShowAsync<IdentificationLoadingDialog>("", loadingParameters, loadingOptions);

        // 获取对话框实例以便更新进度
        var dialogInstance = loadingDialogRef.Dialog as IdentificationLoadingDialog;

        // 订阅进度更新事件
        progressReporter.OnProgress += (entry) =>
        {
            dialogInstance?.HandleProgress(entry);
        };

        // 3. 调用识别服务
        try
        {
            var media = await FilesService.GetMediaByPath(info.SourcePath, identificationOptions, progressReporter, cancellationTokenSource.Token);

            // 关闭加载对话框
            loadingDialogRef.Close();

            if (media != null)
            {
                // 显示媒体信息对话框 —— 由对话框自身完成入库流程并反馈状态
                var mediaParameters = new DialogParameters
                {
                    { nameof(MediaInfoDialog.Media), media },
                    {
                        nameof(MediaInfoDialog.OnConfirmAsync),
                        new Func<Core.Models.Media.MediaBase, Task>(async m => await FilesService.AddMediaToDatabase(m))
                    }
                };

                var mediaOptions = new DialogOptions
                {
                    CloseButton = true,
                    MaxWidth = MaxWidth.Medium,
                    FullWidth = true,
                    Position = DialogPosition.Center
                };

                var mediaDialog = await DialogService.ShowAsync<MediaInfoDialog>("媒体信息", mediaParameters, mediaOptions);
                var mediaResult = await mediaDialog.Result;
                if (mediaResult is { Canceled: false })
                {
                    Snackbar.Add($"添加媒体到数据库成功: {media.Title}", Severity.Success);
                }
            }
            else
            {
                Snackbar.Add("无法识别该文件", Severity.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭加载对话框
            loadingDialogRef.Close();

            Log.Information("识别已被用户取消: {Path}", info.SourcePath);
            Snackbar.Add("识别已取消", Severity.Info);
        }
        catch (Exception ex)
        {
            // 关闭加载对话框
            loadingDialogRef.Close();

            Log.Error(ex, "识别媒体失败: {Path}", info.SourcePath);
            Snackbar.Add("识别失败，请重试", Severity.Error);
        }
    }

    private Color GetStatusColor(bool success) => success ? Color.Success : Color.Error;

    public void Dispose()
    {
        _isDisposed = true;
        _refreshTimer?.Dispose();
    }
}
