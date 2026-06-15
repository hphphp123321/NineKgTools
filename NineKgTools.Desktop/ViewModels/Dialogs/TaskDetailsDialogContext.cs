using Avalonia;
using Avalonia.Media;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// TaskDetailsDialog 视图上下文。聚合 TaskProgress（运行中）+ TaskExecutionInfo（历史归档）
/// 两路数据源——历史优先（字段更全）；都没有时显示"任务已被清理"占位。
///
/// §2.2 P1 任务详情简化版：状态信息 + 时间 + 子任务统计 + 错误 + 日志列表（内联）。
/// 不另起 TaskLogViewer 控件——日志展示控件简单到内联即可。
/// </summary>
public sealed class TaskDetailsDialogContext
{
    public TaskDetailsDialogContext(string taskId, TaskProgress? progress, TaskExecutionInfo? history)
    {
        TaskId = taskId;

        var name = history?.TaskName ?? progress?.TaskName ?? "—";
        TaskName = name;

        TaskTypeText = history?.TaskType.ToString() ?? "—";

        // 状态信息：history 已完成态最准；否则取 progress 实时
        if (history is not null)
        {
            IsCompleted = true;
            Success = history.Success;
            StatusIcon = history.Success ? "✓" : "✕";
            StatusText = history.Success ? "已成功" : "失败";
            StatusBrushKey = history.Success ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush";
            StartTimeText = history.StartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            EndTimeText = history.EndTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            DurationText = FormatDuration(history.Duration);
            ProcessedItems = history.ProcessedItems;
            FailedItems = history.FailedItems;
            HasItemStats = history.ProcessedItems + history.FailedItems > 0;
            ItemStatsText = HasItemStats
                ? $"{history.ProcessedItems} 成功 / {history.FailedItems} 失败"
                : "—";
            ErrorMessage = history.Success ? null : history.Message;
            HasError = !string.IsNullOrEmpty(ErrorMessage);
            SourcePath = history.SourcePath ?? "";
            HasSourcePath = !string.IsNullOrEmpty(SourcePath);

            Logs = history.GetLogEntries() ?? new List<TaskLogEntry>();
            HasLogs = Logs.Count > 0;

            // 历史模式无运行时进度
            ShowProgress = false;
            ProgressPercentage = 100;
            ProgressText = "";
            CurrentItemText = "";
        }
        else if (progress is not null)
        {
            IsCompleted = progress.IsActive == false;
            var status = progress.Status;
            (StatusIcon, StatusText, StatusBrushKey) = MapStatus(status);
            Success = status == TaskExecutionStatus.Succeeded;

            StartTimeText = progress.StartTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
            EndTimeText = progress.EndTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
            var d = progress.Duration ?? (progress.StartTime.HasValue
                ? DateTime.UtcNow - progress.StartTime.Value
                : (TimeSpan?)null);
            DurationText = d.HasValue ? FormatDuration(d.Value) : "—";

            // 实时进度（运行中）
            ShowProgress = progress.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying;
            ProgressPercentage = progress.AggregatedProgressPercentage;
            ProgressText = $"{ProgressPercentage:F0}%";
            CurrentItemText = progress.CurrentItem ?? "";

            // 子任务统计
            var s = progress.ChildrenStats;
            ProcessedItems = s.SucceededCount;
            FailedItems = s.FailedCount;
            HasItemStats = s.TotalCount > 0;
            ItemStatsText = HasItemStats
                ? $"{s.SucceededCount + s.FailedCount + s.CancelledCount + s.SkippedCount} / {s.TotalCount} 子任务完成"
                : "—";

            ErrorMessage = progress.ErrorMessage;
            HasError = !string.IsNullOrEmpty(ErrorMessage);

            SourcePath = "";
            HasSourcePath = false;

            // 运行中任务无 LogEntries 公开 API ——
            Logs = new List<TaskLogEntry>();
            HasLogs = false;
        }
        else
        {
            // 都没有——显示空占位
            IsCompleted = true;
            StatusIcon = "?";
            StatusText = "未找到任务（可能已被清理）";
            StatusBrushKey = "TextFillColorTertiaryBrush";
            StartTimeText = EndTimeText = DurationText = ItemStatsText = "—";
            ErrorMessage = null;
            HasError = false;
            SourcePath = "";
            HasSourcePath = false;
            HasItemStats = false;
            ShowProgress = false;
            ProgressPercentage = 0;
            ProgressText = CurrentItemText = "";
            Logs = new List<TaskLogEntry>();
            HasLogs = false;
        }

        StatusBrush = ResolveBrush(StatusBrushKey);
    }

    public string TaskId { get; }
    public string TaskName { get; }
    public string TaskTypeText { get; }

    public bool IsCompleted { get; }
    public bool Success { get; }
    public string StatusIcon { get; }
    public string StatusText { get; }
    public string StatusBrushKey { get; }
    public IBrush? StatusBrush { get; }

    public string StartTimeText { get; }
    public string EndTimeText { get; }
    public string DurationText { get; }

    public int ProcessedItems { get; }
    public int FailedItems { get; }
    public bool HasItemStats { get; }
    public string ItemStatsText { get; }

    public string? ErrorMessage { get; }
    public bool HasError { get; }

    public string SourcePath { get; }
    public bool HasSourcePath { get; }

    public bool ShowProgress { get; }
    public double ProgressPercentage { get; }
    public string ProgressText { get; }
    public string CurrentItemText { get; }

    public IReadOnlyList<TaskLogEntry> Logs { get; }
    public bool HasLogs { get; }

    private static (string icon, string text, string brushKey) MapStatus(TaskExecutionStatus s) => s switch
    {
        TaskExecutionStatus.Pending => ("⏳", "排队中", "SystemFillColorAttentionBrush"),
        TaskExecutionStatus.Running => ("▶", "运行中", "SystemFillColorAttentionBrush"),
        TaskExecutionStatus.Retrying => ("↻", "重试中", "SystemFillColorAttentionBrush"),
        TaskExecutionStatus.Succeeded => ("✓", "已完成", "SystemFillColorSuccessBrush"),
        TaskExecutionStatus.Failed => ("✕", "失败", "SystemFillColorCriticalBrush"),
        TaskExecutionStatus.Timeout => ("⌛", "超时", "SystemFillColorCriticalBrush"),
        TaskExecutionStatus.Cancelled => ("⊘", "已取消", "TextFillColorTertiaryBrush"),
        TaskExecutionStatus.Skipped => ("↷", "已跳过", "TextFillColorTertiaryBrush"),
        _ => ("·", "未知", "TextFillColorPrimaryBrush"),
    };

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 1) return $"{d.TotalMilliseconds:F0} ms";
        if (d.TotalMinutes < 1) return $"{d.TotalSeconds:F1} s";
        if (d.TotalHours < 1) return $"{d.Minutes}m {d.Seconds}s";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }

    private static IBrush? ResolveBrush(string key) => ResourceLookup.Brush(key);
}
