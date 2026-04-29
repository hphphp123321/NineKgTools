using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;

namespace NineKgTools.Core.Services.Tasks.Base;

/// <summary>
/// 定时任务基类 - 为现有定时任务提供默认实现
/// </summary>
public abstract class ScheduledTaskBase : IScheduledTask
{
    /// <summary>
    /// 任务ID（默认使用GUID）
    /// </summary>
    public virtual string TaskId { get; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 任务名称
    /// </summary>
    public abstract string TaskName { get; }
    
    /// <summary>
    /// 任务类型标识
    /// </summary>
    public abstract TaskType TaskType { get; }
    
    /// <summary>
    /// 任务描述
    /// </summary>
    public virtual string? Description => TaskDescription;
    
    /// <summary>
    /// 任务描述（向后兼容）
    /// </summary>
    public virtual string? TaskDescription => null;
    
    /// <summary>
    /// 任务优先级（定时任务默认为Normal）
    /// </summary>
    public virtual TaskPriority Priority => TaskPriority.Normal;
    
    /// <summary>
    /// 任务参数
    /// </summary>
    public virtual Dictionary<string, object>? Parameters { get; set; }

    // 父子任务关系（来自 ITask 接口）
    public string? ParentTaskId { get; set; }
    public List<string> ChildTaskIds { get; } = new();
    public string? HangfireBatchId { get; set; }

    /// <summary>
    /// Cron表达式
    /// </summary>
    public virtual string? CronExpression { get; set; }
    
    /// <summary>
    /// 最后执行时间
    /// </summary>
    public virtual DateTime? LastExecutionTime { get; set; }
    
    /// <summary>
    /// 下次执行时间
    /// </summary>
    public virtual DateTime? NextExecutionTime { get; set; }
    
    /// <summary>
    /// 执行任务（带进度报告）
    /// </summary>
    public virtual async Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        // 报告任务开始
        await progressReporter.StartAsync($"开始执行: {TaskName}");

        try
        {
            // 调用子类实现
            var result = await ExecuteAsync(Parameters, cancellationToken);

            // 报告任务完成
            if (result.Success)
            {
                await progressReporter.CompleteAsync(result.Message ?? "任务完成", result.ProcessedItems, result.FailedItems);
            }
            else
            {
                await progressReporter.FailAsync(result.Message ?? "任务失败");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            await progressReporter.FailAsync("任务被取消");
            throw;
        }
        catch (Exception ex)
        {
            await progressReporter.FailAsync($"任务执行异常: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// 执行定时任务（子类实现）
    /// </summary>
    public abstract Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证任务参数（旧接口，子类实现）
    /// </summary>
    public abstract bool ValidateParameters(Dictionary<string, object>? parameters);
    
    /// <summary>
    /// 验证任务参数（新接口）
    /// </summary>
    public virtual bool ValidateParameters()
    {
        return ValidateParameters(Parameters);
    }
    
    /// <summary>
    /// 获取预估执行时间
    /// </summary>
    public virtual TimeSpan? GetEstimatedDuration()
    {
        // 定时任务通常没有明确的预估时间
        return null;
    }
}