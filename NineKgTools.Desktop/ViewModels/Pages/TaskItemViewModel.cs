using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 后台任务页每一行 VM。包装 <see cref="TaskProgress"/> 引用，派生派生 UI 字段。
/// 进度更新时由父 VM 调 <see cref="NotifyAll"/> 一次性 refresh 所有 binding。
/// </summary>
public partial class TaskItemViewModel : ObservableObject
{
    public TaskProgress Progress { get; }

    public TaskItemViewModel(TaskProgress progress)
    {
        Progress = progress;
    }

    public string TaskId => Progress.TaskId;
    public string DisplayName => Progress.TaskName;
    public TaskExecutionStatus Status => Progress.Status;
    public double ProgressPercentage => Progress.AggregatedProgressPercentage;
    public string ProgressText => $"{ProgressPercentage:F0}%";
    public string? CurrentMessage => Progress.CurrentMessage;
    public string? CurrentItem => Progress.CurrentItem;

    public bool IsRunning => Status is TaskExecutionStatus.Running or TaskExecutionStatus.Retrying;
    public bool IsPending => Status is TaskExecutionStatus.Pending;
    public bool IsSucceeded => Status == TaskExecutionStatus.Succeeded;
    public bool IsFailed => Status is TaskExecutionStatus.Failed or TaskExecutionStatus.Timeout;
    public bool IsCancelled => Status == TaskExecutionStatus.Cancelled;
    public bool IsCompleted => IsSucceeded || IsFailed || IsCancelled;

    public bool ShowProgressBar => IsRunning || IsPending;
    public bool CanCancel => IsRunning || IsPending;

    /// <summary>子任务统计文本（如有），如 "(12/45 完成)"</summary>
    public string ChildrenStatsText
    {
        get
        {
            var s = Progress.ChildrenStats;
            if (s.TotalCount == 0) return "";
            var done = s.SucceededCount + s.FailedCount + s.CancelledCount + s.SkippedCount;
            return $"({done}/{s.TotalCount} 完成)";
        }
    }

    public bool HasChildren => Progress.ChildrenStats.TotalCount > 0;

    public string StatusIcon => Status switch
    {
        TaskExecutionStatus.Pending => "⏳",
        TaskExecutionStatus.Running or TaskExecutionStatus.Retrying => "▶",
        TaskExecutionStatus.Succeeded => "✓",
        TaskExecutionStatus.Failed or TaskExecutionStatus.Timeout => "✕",
        TaskExecutionStatus.Cancelled => "⊘",
        TaskExecutionStatus.Skipped => "↷",
        _ => "·"
    };

    public string StatusText => Status switch
    {
        TaskExecutionStatus.Pending => "排队中",
        TaskExecutionStatus.Running => "运行中",
        TaskExecutionStatus.Retrying => $"重试中 {Progress.RetryInfo}",
        TaskExecutionStatus.Succeeded => "已完成",
        TaskExecutionStatus.Failed => "失败",
        TaskExecutionStatus.Timeout => "超时",
        TaskExecutionStatus.Cancelled => "已取消",
        TaskExecutionStatus.Skipped => "已跳过",
        _ => "未知"
    };

    /// <summary>状态色 brush key（运行=蓝，完成=绿，失败=红，取消=灰）</summary>
    public IBrush? StatusBrush
    {
        get
        {
            string key = Status switch
            {
                TaskExecutionStatus.Pending or TaskExecutionStatus.Running or TaskExecutionStatus.Retrying
                    => "SystemFillColorAttentionBrush",
                TaskExecutionStatus.Succeeded => "SystemFillColorSuccessBrush",
                TaskExecutionStatus.Failed or TaskExecutionStatus.Timeout => "SystemFillColorCriticalBrush",
                TaskExecutionStatus.Cancelled or TaskExecutionStatus.Skipped => "TextFillColorTertiaryBrush",
                _ => "TextFillColorPrimaryBrush"
            };
            if (Application.Current?.Resources.TryGetResource(
                    key, Application.Current.ActualThemeVariant, out var b) == true && b is IBrush br)
                return br;
            return null;
        }
    }

    public string TimingText
    {
        get
        {
            var d = Progress.Duration;
            if (d is null && Progress.StartTime.HasValue)
            {
                d = DateTime.UtcNow - Progress.StartTime.Value;
            }
            if (d is null) return "";

            var elapsedStr = FormatDuration(d.Value);
            if (IsCompleted) return $"耗时 {elapsedStr}";

            if (Progress.EstimatedTimeRemaining is { } eta && eta.TotalSeconds > 0)
                return $"已用 {elapsedStr} / 预计还 {FormatDuration(eta)}";

            return $"已用 {elapsedStr}";
        }
    }

    public string? ErrorMessage => Progress.ErrorMessage;
    public bool HasErrorMessage => !string.IsNullOrEmpty(Progress.ErrorMessage);

    /// <summary>
    /// 是否有识别诊断（仅 SingleSourceIdentificationTask 上下文里的任务会有）。
    /// 用于决定是否在行动作组里显示"诊断"按钮。
    /// </summary>
    public bool HasDiagnostics => Progress.IdentificationDiagnostics is not null;

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalSeconds < 60) return $"{ts.TotalSeconds:F0}s";
        if (ts.TotalMinutes < 60) return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalHours}h {ts.Minutes}m";
    }

    /// <summary>父 VM 在 progress 字段刷新后调用，触发所有派生 binding 重新拉值</summary>
    public void NotifyAll()
    {
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ProgressPercentage));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CurrentMessage));
        OnPropertyChanged(nameof(CurrentItem));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(IsSucceeded));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(IsCancelled));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(ShowProgressBar));
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(ChildrenStatsText));
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(TimingText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasErrorMessage));
        OnPropertyChanged(nameof(HasDiagnostics));
    }
}
