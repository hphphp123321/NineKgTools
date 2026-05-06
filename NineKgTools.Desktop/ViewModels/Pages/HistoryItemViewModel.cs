using Avalonia.Media;
using NineKgTools.Core.Services.Tasks;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// BackgroundTasksPage "历史" Tab 的行 VM——纯只读包装 TaskExecutionInfo。
/// 字段格式化为可绑定的 string，AXAML 直接显示，不需要 converter。
/// </summary>
public sealed class HistoryItemViewModel
{
    public HistoryItemViewModel(TaskExecutionInfo info)
    {
        TaskId = info.TaskId;
        TaskName = info.TaskName;
        TaskTypeText = info.TaskType.ToString();
        Success = info.Success;
        StatusIcon = info.Success ? "✓" : "✕";
        StatusText = info.Success ? "成功" : "失败";

        StartTimeText = info.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
        DurationText = FormatDuration(info.Duration);

        ProcessedItems = info.ProcessedItems;
        FailedItems = info.FailedItems;
        StatsText = info.ProcessedItems + info.FailedItems > 0
            ? $"{info.ProcessedItems} 成功 / {info.FailedItems} 失败"
            : "—";

        Message = info.Message ?? "";
        SourcePath = info.SourcePath ?? "";
        HasSourcePath = !string.IsNullOrEmpty(SourcePath);

        StatusBrush = info.Success
            ? AppBrush("SystemFillColorSuccessBrush")
            : AppBrush("SystemFillColorCriticalBrush");
    }

    public string TaskId { get; }
    public string TaskName { get; }
    public string TaskTypeText { get; }
    public bool Success { get; }
    public string StatusIcon { get; }
    public string StatusText { get; }
    public IBrush? StatusBrush { get; }

    public string StartTimeText { get; }
    public string DurationText { get; }

    public int ProcessedItems { get; }
    public int FailedItems { get; }
    public string StatsText { get; }

    public string Message { get; }
    public string SourcePath { get; }
    public bool HasSourcePath { get; }

    private static string FormatDuration(TimeSpan d)
    {
        if (d.TotalSeconds < 1) return $"{d.TotalMilliseconds:F0} ms";
        if (d.TotalMinutes < 1) return $"{d.TotalSeconds:F1} s";
        if (d.TotalHours < 1) return $"{d.Minutes}m {d.Seconds}s";
        return $"{(int)d.TotalHours}h {d.Minutes}m";
    }

    private static IBrush? AppBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                key, Avalonia.Application.Current.ActualThemeVariant, out var obj) == true
            && obj is IBrush b)
        {
            return b;
        }
        return null;
    }
}
