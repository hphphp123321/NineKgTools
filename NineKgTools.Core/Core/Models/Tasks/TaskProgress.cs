using NineKgTools.Core.Models.Tasks.Diagnostics;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务进度信息
/// </summary>
public class TaskProgress
{
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public TaskType? TaskType { get; set; }
    public TaskExecutionStatus Status { get; set; }
    public double ProgressPercentage { get; set; }
    public string? CurrentPhase { get; set; }
    public string? CurrentMessage { get; set; }
    public string? CurrentItem { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// 识别诊断信息（仅识别类任务有）。
    /// 由 <see cref="SingleSourceIdentificationTask"/> 在执行起头通过
    /// <c>TaskProgressService.AttachDiagnostics</c> 挂载同一引用，运行中也能实时读到累积的诊断。
    /// </summary>
    public IdentificationDiagnostics? IdentificationDiagnostics { get; set; }

    // 日志缓冲区
    private TaskLogBuffer? _logBuffer;

    /// <summary>
    /// 日志缓冲区
    /// </summary>
    public TaskLogBuffer LogBuffer => _logBuffer ??= new TaskLogBuffer();

    /// <summary>
    /// 日志条目只读列表
    /// </summary>
    public IReadOnlyList<TaskLogEntry> LogEntries => LogBuffer.GetAll();

    /// <summary>
    /// 添加日志条目
    /// </summary>
    public void AddLogEntry(TaskLogEntry entry)
    {
        LogBuffer.Add(entry);
    }

    // 父子任务关系
    public string? ParentTaskId { get; set; }
    public List<TaskProgress> ChildTasks { get; set; } = new();

    // 重试相关字段
    /// <summary>
    /// 当前重试次数
    /// </summary>
    public int CurrentRetry { get; set; }

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; }

    /// <summary>
    /// 重试信息文本（如 "2/3"）
    /// </summary>
    public string? RetryInfo => MaxRetries > 0 ? $"{CurrentRetry}/{MaxRetries}" : null;

    public TimeSpan? Duration => EndTime.HasValue && StartTime.HasValue ?
        EndTime.Value - StartTime.Value : null;

    public bool IsActive => Status == TaskExecutionStatus.Running || Status == TaskExecutionStatus.Pending;

    /// <summary>
    /// 是否是父任务（有子任务）
    /// </summary>
    public bool IsParentTask => ChildTasks.Any();

    /// <summary>
    /// 聚合进度百分比（包括所有子任务）
    /// 如果是父任务，返回所有子任务的平均进度；否则返回自身进度
    /// </summary>
    public double AggregatedProgressPercentage
    {
        get
        {
            if (!ChildTasks.Any())
                return ProgressPercentage;

            // 递归计算所有子任务的平均进度
            return ChildTasks.Average(child => child.AggregatedProgressPercentage);
        }
    }

    /// <summary>
    /// 子任务状态统计
    /// </summary>
    public TaskChildrenStats ChildrenStats
    {
        get
        {
            if (!ChildTasks.Any())
                return new TaskChildrenStats();

            return new TaskChildrenStats
            {
                TotalCount = ChildTasks.Count,
                PendingCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Pending),
                RunningCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Running),
                SucceededCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Succeeded),
                FailedCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Failed),
                CancelledCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Cancelled),
                SkippedCount = ChildTasks.Count(c => c.Status == TaskExecutionStatus.Skipped)
            };
        }
    }
}
