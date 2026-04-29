using System;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务日志级别
/// </summary>
public enum TaskLogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    Success
}

/// <summary>
/// 任务日志条目
/// </summary>
public class TaskLogEntry
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 日志级别
    /// </summary>
    public TaskLogLevel Level { get; set; }

    /// <summary>
    /// 日志消息
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// 当前处理项（可选）
    /// </summary>
    public string? CurrentItem { get; set; }

    /// <summary>
    /// 当前进度（可选，0-100）
    /// </summary>
    public double? Progress { get; set; }
}
