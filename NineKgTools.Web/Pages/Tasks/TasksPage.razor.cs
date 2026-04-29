using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Core.Services.Files;
using Serilog;

namespace NineKgTools.Pages.Tasks;

public partial class TasksPage : ComponentBase, IDisposable
{
    [Inject] private UnifiedTaskService TaskService { get; set; } = null!;
    [Inject] private TaskProgressService ProgressService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private MonitorService MonitorService { get; set; } = null!;
    [Inject] private ScheduledTaskFactory ScheduledTaskFactory { get; set; } = null!;

    // 运行中的任务
    private List<TaskProgress> _runningTasks = new();

    // 后台任务
    private List<BackgroundTaskInfo> _backgroundTasks = new();

    // 统计信息
    private TaskStatistics _statistics = new();

    // 定时任务数量
    private int _scheduledTaskCount;

    // 加载状态（仅用于首次加载）
    private bool _isLoading = true;

    // 定时刷新器
    private System.Threading.Timer? _refreshTimer;
    private bool _isDisposed;

    protected override async Task OnInitializedAsync()
    {
        await RefreshDataAsync(isInitialLoad: true);

        // 启动定时刷新（每2秒静默刷新）
        _refreshTimer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
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
            Log.Warning(ex, "定时刷新任务数据时出错");
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

            // 只获取根任务（没有父任务的任务）
            // GetAllRootTasks()会自动加载完整的子任务树
            _runningTasks = ProgressService.GetAllRootTasks()
                .Where(t => t.IsActive)
                .ToList();

            // 获取统计信息
            _statistics = TaskService.GetStatistics();

            // 获取定时任务数量
            _scheduledTaskCount = ScheduledTaskFactory.GetAllTaskMetadata().Count;

            // 加载后台任务
            LoadBackgroundTasks();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新任务数据失败");
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
                TaskName = $"监控: {System.IO.Path.GetFileName(mt.FolderPath)}",
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
    /// 手动刷新
    /// </summary>
    private async Task ManualRefreshAsync()
    {
        await RefreshDataAsync();
        Snackbar.Add("刷新成功", Severity.Success);
    }

    /// <summary>
    /// 取消任务（从子组件调用）
    /// </summary>
    private async Task CancelTaskFromChildAsync(string taskId)
    {
        var taskName = _runningTasks.FirstOrDefault(t => t.TaskId == taskId)?.TaskName
                       ?? ProgressService.GetProgress(taskId)?.TaskName
                       ?? "未知任务";

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "取消任务",
            "任务可能无法立即停止，会在下次检查点响应取消。",
            intent: ConfirmIntent.Info,
            confirmText: "取消任务",
            cancelText: "继续执行",
            targetName: taskName,
            targetIcon: Icons.Material.Filled.PendingActions,
            icon: Icons.Material.Filled.StopCircle);

        if (!confirmed) return;

        bool success = TaskService.CancelTask(taskId);
        if (success)
        {
            Snackbar.Add("任务取消指令已发送", Severity.Success);
            await RefreshDataAsync();
        }
        else
        {
            Snackbar.Add("取消任务失败，任务可能已完成", Severity.Warning);
        }
    }

    /// <summary>
    /// 显示任务详情对话框
    /// </summary>
    private async Task ShowTaskDetailsAsync(string taskId)
    {
        var parameters = new DialogParameters { ["TaskId"] = taskId };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true
        };

        await DialogService.ShowAsync<NineKgTools.Components.Tasks.TaskDetailsDialog>("任务详情", parameters, options);
    }

    public void Dispose()
    {
        _isDisposed = true;
        _refreshTimer?.Dispose();
    }
}
