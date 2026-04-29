using Microsoft.AspNetCore.Components;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Components.Tasks;

/// <summary>
/// 任务日志查看器组件
/// </summary>
public partial class TaskLogViewer
{
    /// <summary>
    /// 日志条目列表
    /// </summary>
    [Parameter]
    public IReadOnlyList<TaskLogEntry> LogEntries { get; set; } = Array.Empty<TaskLogEntry>();

    /// <summary>
    /// 是否自动滚动到底部
    /// </summary>
    [Parameter]
    public bool AutoScroll { get; set; } = true;

    /// <summary>
    /// 自动滚动变化事件
    /// </summary>
    [Parameter]
    public EventCallback<bool> AutoScrollChanged { get; set; }

    /// <summary>
    /// 额外的CSS类
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 内联样式
    /// </summary>
    [Parameter]
    public string? Style { get; set; }

    /// <summary>
    /// 选中的日志级别筛选
    /// </summary>
    private TaskLogLevel? SelectedLevel { get; set; }

    /// <summary>
    /// 日志容器引用
    /// </summary>
    private ElementReference _logContainerRef;

    /// <summary>
    /// 过滤后的日志列表
    /// </summary>
    private IReadOnlyList<TaskLogEntry> FilteredLogs => SelectedLevel.HasValue
        ? LogEntries.Where(l => l.Level == SelectedLevel.Value).ToList()
        : LogEntries.ToList();

    /// <summary>
    /// 切换自动滚动
    /// </summary>
    private async Task ToggleAutoScroll()
    {
        AutoScroll = !AutoScroll;
        await AutoScrollChanged.InvokeAsync(AutoScroll);
    }

    /// <summary>
    /// 清除筛选
    /// </summary>
    private void ClearFilter()
    {
        SelectedLevel = null;
    }

    /// <summary>
    /// 获取日志级别显示文本
    /// </summary>
    private static string GetLevelText(TaskLogLevel level) => level switch
    {
        TaskLogLevel.Debug => "DEBUG",
        TaskLogLevel.Info => "INFO",
        TaskLogLevel.Warning => "WARN",
        TaskLogLevel.Error => "ERROR",
        TaskLogLevel.Success => "OK",
        _ => "UNKNOWN"
    };
}
