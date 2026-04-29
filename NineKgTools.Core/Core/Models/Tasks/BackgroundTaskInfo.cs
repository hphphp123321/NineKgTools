namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 后台任务信息（用于 UI 展示）
/// </summary>
public class BackgroundTaskInfo
{
    public required string TaskId { get; set; }
    public required string TaskName { get; set; }
    public required string FolderPath { get; set; }
    public DateTime StartTime { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// 后台任务统计信息
/// </summary>
public class BackgroundTaskStatistics
{
    public int RunningCount { get; set; }
    public int TotalProcessed { get; set; }
    public int TotalFailed { get; set; }
    public TimeSpan LongestRunningTime { get; set; }
}
