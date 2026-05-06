using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Desktop.Views.Windows;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public enum TaskStatusFilter { All, Running, Succeeded, Failed }

public partial class BackgroundTasksViewModel : PageViewModelBase
{
    private readonly TaskProgressService _progressService;
    private readonly UnifiedTaskService _taskService;
    private DispatcherTimer? _refreshTimer;

    public override string Title => "任务";

    [ObservableProperty]
    private ObservableCollection<TaskItemViewModel> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsFilterRunning))]
    [NotifyPropertyChangedFor(nameof(IsFilterSucceeded))]
    [NotifyPropertyChangedFor(nameof(IsFilterFailed))]
    private TaskStatusFilter _selectedFilter = TaskStatusFilter.All;

    [ObservableProperty]
    private int _allCount;

    [ObservableProperty]
    private int _runningCount;

    [ObservableProperty]
    private int _succeededCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private bool _showEmpty = true;

    public bool IsFilterAll => SelectedFilter == TaskStatusFilter.All;
    public bool IsFilterRunning => SelectedFilter == TaskStatusFilter.Running;
    public bool IsFilterSucceeded => SelectedFilter == TaskStatusFilter.Succeeded;
    public bool IsFilterFailed => SelectedFilter == TaskStatusFilter.Failed;

    public BackgroundTasksViewModel(TaskProgressService progressService, UnifiedTaskService taskService)
    {
        _progressService = progressService;
        _taskService = taskService;
    }

    public override Task OnEnterAsync()
    {
        Refresh();
        // 500ms 轮询足够实时，开销远小于 Subscribe + 跨 task 注销/订阅
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _refreshTimer.Tick += (_, _) => Refresh();
        _refreshTimer.Start();
        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void SelectFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return;
        if (Enum.TryParse<TaskStatusFilter>(filter, ignoreCase: true, out var f))
        {
            SelectedFilter = f;
            Refresh();
        }
    }

    [RelayCommand]
    private async Task CancelTaskAsync(TaskItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            // CancelTaskAsync 处理 Hangfire 排队任务；CancelTask 处理运行中
            await _taskService.CancelTaskAsync(item.TaskId);
            _taskService.CancelTask(item.TaskId);
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消任务失败：{Id}", item.TaskId);
        }
    }

    [RelayCommand]
    private void OpenDiagnostics(TaskItemViewModel? item)
    {
        if (item is null) return;

        // 优先取 live 上挂的诊断（运行中 + 刚完成都在）
        var diagnostics = item.Progress.IdentificationDiagnostics;

        // live 没有就回退到执行历史（如果用户已经 ClearCompleted 过、TaskProgress 被清掉的情况）
        if (diagnostics is null)
        {
            var history = _taskService.GetExecutionHistory()
                .FirstOrDefault(h => h.TaskId == item.TaskId);
            diagnostics = history?.GetIdentificationDiagnostics();
        }

        try
        {
            var window = new TaskDiagnosticsWindow(diagnostics, item.DisplayName);

            // 找到主窗当 Owner，保证 z-order + 居中
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
                && lifetime.MainWindow is not null)
            {
                window.Show(lifetime.MainWindow);
            }
            else
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开识别诊断窗口失败：{Id}", item.TaskId);
        }
    }

    [RelayCommand]
    private void ClearCompleted()
    {
        // 清理 1 分钟前完成的任务（保留最近的让用户能看到刚完成的状态）
        try
        {
            _progressService.CleanupCompletedTasks(TimeSpan.FromMinutes(1));
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清理已完成任务失败");
        }
    }

    /// <summary>差量更新：相同 taskId 的项就地刷新，新增则插入，消失则移除</summary>
    private void Refresh()
    {
        try
        {
            var allTasks = _progressService.GetAllRootTasks().ToList();

            // 各分类计数（基于全集，不受过滤影响）
            AllCount = allTasks.Count;
            RunningCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying);
            SucceededCount = allTasks.Count(t => t.Status == TaskExecutionStatus.Succeeded);
            FailedCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Failed
                or TaskExecutionStatus.Timeout
                or TaskExecutionStatus.Cancelled);

            // 应用 filter
            var filtered = SelectedFilter switch
            {
                TaskStatusFilter.Running => allTasks.Where(t => t.Status is TaskExecutionStatus.Running
                    or TaskExecutionStatus.Pending
                    or TaskExecutionStatus.Retrying),
                TaskStatusFilter.Succeeded => allTasks.Where(t => t.Status == TaskExecutionStatus.Succeeded),
                TaskStatusFilter.Failed => allTasks.Where(t => t.Status is TaskExecutionStatus.Failed
                    or TaskExecutionStatus.Timeout
                    or TaskExecutionStatus.Cancelled),
                _ => allTasks,
            };

            // 排序：运行中 / 排队 → 完成 → 失败 → 取消（同状态按 StartTime 倒序）
            var sorted = filtered
                .OrderBy(t => StatusOrder(t.Status))
                .ThenByDescending(t => t.StartTime ?? DateTime.MinValue)
                .ToList();

            // 差量更新 Items
            var freshIds = sorted.Select(t => t.TaskId).ToHashSet();

            // 1. 移除消失的
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!freshIds.Contains(Items[i].TaskId)) Items.RemoveAt(i);
            }

            // 2. 更新 + 按目标顺序插入新项
            for (int i = 0; i < sorted.Count; i++)
            {
                var fresh = sorted[i];
                var existingIdx = -1;
                for (int j = 0; j < Items.Count; j++)
                {
                    if (Items[j].TaskId == fresh.TaskId) { existingIdx = j; break; }
                }

                if (existingIdx == -1)
                {
                    Items.Insert(i, new TaskItemViewModel(fresh));
                }
                else
                {
                    if (existingIdx != i) Items.Move(existingIdx, i);
                    Items[i].NotifyAll();
                }
            }

            ShowEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "BackgroundTasks Refresh 失败");
        }
    }

    private static int StatusOrder(TaskExecutionStatus s) => s switch
    {
        TaskExecutionStatus.Running or TaskExecutionStatus.Retrying => 0,
        TaskExecutionStatus.Pending => 1,
        TaskExecutionStatus.Succeeded => 2,
        TaskExecutionStatus.Failed or TaskExecutionStatus.Timeout => 3,
        TaskExecutionStatus.Cancelled or TaskExecutionStatus.Skipped => 4,
        _ => 5
    };
}
