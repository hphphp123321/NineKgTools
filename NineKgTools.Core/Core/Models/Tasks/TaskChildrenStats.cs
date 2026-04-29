namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 子任务统计信息
/// 用于父任务中汇总所有子任务的状态分布
/// </summary>
public class TaskChildrenStats
{
    /// <summary>
    /// 子任务总数
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// 待处理的子任务数
    /// </summary>
    public int PendingCount { get; set; }

    /// <summary>
    /// 正在运行的子任务数
    /// </summary>
    public int RunningCount { get; set; }

    /// <summary>
    /// 已成功完成的子任务数
    /// </summary>
    public int SucceededCount { get; set; }

    /// <summary>
    /// 失败的子任务数
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 已取消的子任务数
    /// </summary>
    public int CancelledCount { get; set; }

    /// <summary>
    /// 已跳过的子任务数
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// 完成率（百分比，0-100）
    /// </summary>
    public double CompletionPercentage
    {
        get
        {
            if (TotalCount == 0) return 0;
            var completedCount = SucceededCount + FailedCount + CancelledCount + SkippedCount;
            return (double)completedCount / TotalCount * 100;
        }
    }

    /// <summary>
    /// 成功率（百分比，0-100）
    /// </summary>
    public double SuccessPercentage
    {
        get
        {
            if (TotalCount == 0) return 0;
            return (double)SucceededCount / TotalCount * 100;
        }
    }

    /// <summary>
    /// 是否所有子任务都已完成
    /// </summary>
    public bool AllCompleted => TotalCount > 0 && (SucceededCount + FailedCount + CancelledCount + SkippedCount) == TotalCount;

    /// <summary>
    /// 是否有失败的子任务
    /// </summary>
    public bool HasFailures => FailedCount > 0;

    /// <summary>
    /// 是否所有子任务都成功
    /// </summary>
    public bool AllSucceeded => TotalCount > 0 && SucceededCount == TotalCount;
}
