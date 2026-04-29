using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 定时任务接口 - 用于Hangfire等调度框架执行的定时任务
/// </summary>
public interface IScheduledTask : ITask
{
    /// <summary>
    /// Cron表达式
    /// </summary>
    string? CronExpression { get; }
    
    /// <summary>
    /// 最后执行时间
    /// </summary>
    DateTime? LastExecutionTime { get; set; }
    
    /// <summary>
    /// 下次执行时间
    /// </summary>
    DateTime? NextExecutionTime { get; set; }
    
    /// <summary>
    /// 执行定时任务（向后兼容，不带进度报告）
    /// </summary>
    /// <param name="parameters">任务参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
}