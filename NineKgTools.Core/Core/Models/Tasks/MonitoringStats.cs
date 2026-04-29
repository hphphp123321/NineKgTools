using System;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 监控统计信息
/// </summary>
public class MonitoringStats
{
    /// <summary>
    /// 已处理文件数量
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// 失败文件数量
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime? LastActivityTime { get; set; }

    /// <summary>
    /// 监控开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
}

/// <summary>
/// 监控任务信息
/// </summary>
public class MonitoringTaskInfo
{
    /// <summary>
    /// 文件夹路径
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// 任务ID
    /// </summary>
    public required string TaskId { get; set; }

    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 已处理数量
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int FailedCount { get; set; }
}
