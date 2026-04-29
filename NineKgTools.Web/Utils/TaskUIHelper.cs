using MudBlazor;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Utils;

/// <summary>
/// 任务相关 UI 辅助方法（状态颜色、图标、文本、格式化等）
/// </summary>
public static class TaskUIHelper
{
    /// <summary>
    /// 获取任务状态对应的 MudBlazor 颜色
    /// </summary>
    public static Color GetStatusColor(TaskExecutionStatus status) => status switch
    {
        TaskExecutionStatus.Pending => Color.Default,
        TaskExecutionStatus.Running => Color.Primary,
        TaskExecutionStatus.Succeeded => Color.Success,
        TaskExecutionStatus.Failed => Color.Error,
        TaskExecutionStatus.Cancelled => Color.Warning,
        TaskExecutionStatus.Skipped => Color.Info,
        TaskExecutionStatus.Retrying => Color.Warning,
        _ => Color.Default
    };

    /// <summary>
    /// 获取任务状态对应的中文文本
    /// </summary>
    public static string GetStatusText(TaskExecutionStatus status, string? retryInfo = null) => status switch
    {
        TaskExecutionStatus.Pending => "待处理",
        TaskExecutionStatus.Running => "运行中",
        TaskExecutionStatus.Succeeded => "成功",
        TaskExecutionStatus.Failed => "失败",
        TaskExecutionStatus.Cancelled => "已取消",
        TaskExecutionStatus.Skipped => "已跳过",
        TaskExecutionStatus.Timeout => "超时",
        TaskExecutionStatus.Retrying => retryInfo != null ? $"重试中 ({retryInfo})" : "重试中",
        _ => "未知"
    };

    /// <summary>
    /// 获取任务状态对应的 Material 图标
    /// </summary>
    public static string GetStatusIcon(TaskExecutionStatus status) => status switch
    {
        TaskExecutionStatus.Pending => Icons.Material.Filled.Schedule,
        TaskExecutionStatus.Running => Icons.Material.Filled.PlayCircle,
        TaskExecutionStatus.Succeeded => Icons.Material.Filled.CheckCircle,
        TaskExecutionStatus.Failed => Icons.Material.Filled.Error,
        TaskExecutionStatus.Cancelled => Icons.Material.Filled.Cancel,
        TaskExecutionStatus.Skipped => Icons.Material.Filled.SkipNext,
        TaskExecutionStatus.Retrying => Icons.Material.Filled.Replay,
        _ => Icons.Material.Filled.HelpOutline
    };

    /// <summary>
    /// 获取任务状态对应的 CSS 边框颜色变量
    /// </summary>
    public static string GetBorderColor(TaskExecutionStatus status) => status switch
    {
        TaskExecutionStatus.Running => "var(--mud-palette-primary)",
        TaskExecutionStatus.Succeeded => "var(--mud-palette-success)",
        TaskExecutionStatus.Failed => "var(--mud-palette-error)",
        TaskExecutionStatus.Cancelled => "var(--mud-palette-warning)",
        TaskExecutionStatus.Retrying => "var(--mud-palette-warning)",
        _ => "var(--mud-palette-lines-default)"
    };

    /// <summary>
    /// 格式化可空时间跨度（用于预计剩余时间等）
    /// </summary>
    public static string FormatTimeSpan(TimeSpan? timeSpan)
    {
        if (timeSpan == null) return "--";

        if (timeSpan.Value.TotalHours >= 1)
            return timeSpan.Value.ToString(@"hh\:mm\:ss");

        return timeSpan.Value.ToString(@"mm\:ss");
    }

    /// <summary>
    /// 格式化执行耗时
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return duration.ToString(@"h\:mm\:ss");
        if (duration.TotalMinutes >= 1)
            return duration.ToString(@"m\:ss");
        return duration.ToString(@"s\.f\s");
    }

    /// <summary>
    /// 格式化可空日期时间（自动把 UTC 转本地时区显示）
    /// </summary>
    public static string FormatDateTime(DateTime? dateTime)
    {
        if (dateTime == null) return "--";
        return dateTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }
}
