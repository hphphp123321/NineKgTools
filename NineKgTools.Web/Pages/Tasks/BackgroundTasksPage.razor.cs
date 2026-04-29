using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using Serilog;
using System.Diagnostics;

namespace NineKgTools.Pages.Tasks;

public partial class BackgroundTasksPage : ComponentBase, IDisposable
{
    [Inject] private MonitorService MonitorService { get; set; } = null!;
    [Inject] private TaskProgressService ProgressService { get; set; } = null!;
    [Inject] private UnifiedTaskService TaskService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;

    // 后台任务列表
    private List<BackgroundTaskInfo> _backgroundTasks = new();

    // 统计信息
    private BackgroundTaskStatistics _statistics = new();

    // 加载状态（仅用于首次加载）
    private bool _isLoading = true;

    // 正在停止的任务（防重复点击）
    private readonly HashSet<string> _stoppingTasks = new();

    // 定时刷新器
    private System.Threading.Timer? _refreshTimer;
    private bool _isDisposed;

    protected override async Task OnInitializedAsync()
    {
        await RefreshDataAsync(isInitialLoad: true);

        // 启动定时刷新（每5秒静默刷新）
        _refreshTimer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    private async void OnTimerCallback(object? state)
    {
        if (_isDisposed) return;

        try
        {
            await RefreshDataAsync(isInitialLoad: false);
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
            // 组件已释放，忽略
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "定时刷新后台任务数据时出错");
        }
    }

    /// <summary>
    /// 刷新所有数据
    /// </summary>
    private Task RefreshDataAsync(bool isInitialLoad = false)
    {
        try
        {
            if (isInitialLoad)
                _isLoading = true;

            LoadBackgroundTasks();
            UpdateStatistics();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新后台任务数据失败");
            if (isInitialLoad)
                Snackbar.Add("刷新失败", Severity.Error);
        }
        finally
        {
            if (isInitialLoad)
                _isLoading = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 加载后台任务
    /// </summary>
    private void LoadBackgroundTasks()
    {
        try
        {
            var monitoringTasks = MonitorService.GetAllMonitoringTasks();

            _backgroundTasks = monitoringTasks.Select(mt => new BackgroundTaskInfo
            {
                TaskId = mt.TaskId,
                TaskName = $"监控: {Path.GetFileName(mt.FolderPath)}",
                FolderPath = mt.FolderPath,
                StartTime = mt.StartTime,
                ProcessedCount = mt.ProcessedCount,
                FailedCount = mt.FailedCount
            }).ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载后台任务失败");
        }
    }

    /// <summary>
    /// 更新统计信息
    /// </summary>
    private void UpdateStatistics()
    {
        _statistics = new BackgroundTaskStatistics
        {
            RunningCount = _backgroundTasks.Count,
            TotalProcessed = _backgroundTasks.Sum(t => t.ProcessedCount),
            TotalFailed = _backgroundTasks.Sum(t => t.FailedCount),
            LongestRunningTime = _backgroundTasks.Any()
                ? _backgroundTasks.Max(t => DateTime.UtcNow - t.StartTime)
                : TimeSpan.Zero
        };
    }

    /// <summary>
    /// 手动刷新
    /// </summary>
    private async Task ManualRefreshAsync()
    {
        await RefreshDataAsync();
        Snackbar.Add("刷新成功", Severity.Success);
    }

    /// <summary>
    /// 显示任务详情
    /// </summary>
    private async Task ShowTaskDetailsAsync(BackgroundTaskInfo task)
    {
        var progress = ProgressService.GetProgress(task.TaskId);
        if (progress != null)
        {
            var parameters = new DialogParameters { ["TaskId"] = task.TaskId };
            var options = new DialogOptions
            {
                MaxWidth = MaxWidth.Medium,
                FullWidth = true,
                CloseButton = true
            };

            await DialogService.ShowAsync<NineKgTools.Components.Tasks.TaskDetailsDialog>("任务详情", parameters, options);
        }
        else
        {
            Snackbar.Add("无法获取任务详情", Severity.Warning);
        }
    }

    /// <summary>
    /// 停止任务
    /// </summary>
    private async Task StopTaskAsync(BackgroundTaskInfo task)
    {
        if (_stoppingTasks.Contains(task.TaskId)) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "停止后台任务",
            "停止后任务会在下次检查点退出，已完成的阶段不会回滚。",
            intent: ConfirmIntent.Info,
            confirmText: "停止",
            cancelText: "继续运行",
            targetName: task.TaskName,
            targetIcon: Icons.Material.Filled.Memory,
            icon: Icons.Material.Filled.StopCircle);

        if (!confirmed) return;

        _stoppingTasks.Add(task.TaskId);
        StateHasChanged();

        try
        {
            bool success = TaskService.CancelTask(task.TaskId);
            if (success)
            {
                Snackbar.Add("后台任务已停止", Severity.Success);
                await RefreshDataAsync();
            }
            else
            {
                Snackbar.Add("停止后台任务失败", Severity.Error);
            }
        }
        finally
        {
            _stoppingTasks.Remove(task.TaskId);
            StateHasChanged();
        }
    }

    /// <summary>
    /// 任务是否正在停止中
    /// </summary>
    private bool IsTaskStopping(string taskId) => _stoppingTasks.Contains(taskId);

    /// <summary>
    /// 计算成功率百分比
    /// </summary>
    private static double GetSuccessRate(BackgroundTaskInfo task)
        => task.ProcessedCount > 0
            ? (task.ProcessedCount - task.FailedCount) * 100.0 / task.ProcessedCount
            : 0;

    /// <summary>
    /// 打开文件夹
    /// </summary>
    private Task OpenFolderAsync(string folderPath)
    {
        try
        {
            if (Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });
            }
            else
            {
                Snackbar.Add("文件夹不存在", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开文件夹失败: {Path}", folderPath);
            Snackbar.Add("打开文件夹失败", Severity.Error);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _isDisposed = true;
        _refreshTimer?.Dispose();
    }
}
