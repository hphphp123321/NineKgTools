using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 基础任务接口 - 所有任务类型的基类
/// </summary>
public interface ITask
{
    /// <summary>
    /// 任务唯一标识符
    /// </summary>
    string TaskId { get; }
    
    /// <summary>
    /// 任务名称
    /// </summary>
    string TaskName { get; }
    
    /// <summary>
    /// 任务类型标识
    /// </summary>
    TaskType TaskType { get; }
    
    /// <summary>
    /// 任务描述
    /// </summary>
    string? Description { get; }
    
    /// <summary>
    /// 任务优先级
    /// </summary>
    TaskPriority Priority { get; }
    
    /// <summary>
    /// 任务参数
    /// </summary>
    Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// 父任务ID（如果这是一个子任务）
    /// </summary>
    string? ParentTaskId { get; set; }

    /// <summary>
    /// 子任务ID列表（如果这是一个父任务）
    /// </summary>
    List<string> ChildTaskIds { get; }

    /// <summary>
    /// Hangfire 批次ID（用于管理父子任务）
    /// </summary>
    string? HangfireBatchId { get; set; }

    /// <summary>
    /// 是否是父任务（只读属性，根据是否有子任务判断）
    /// </summary>
    bool IsParentTask => ChildTaskIds != null && ChildTaskIds.Count > 0;

    /// <summary>
    /// 执行任务（带进度报告）
    /// </summary>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务执行结果</returns>
    Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 验证任务参数
    /// </summary>
    /// <returns>参数是否有效</returns>
    bool ValidateParameters();
    
    /// <summary>
    /// 获取预估执行时间
    /// </summary>
    /// <returns>预估执行时间</returns>
    TimeSpan? GetEstimatedDuration();

    /// <summary>
    /// 任务执行前的钩子方法
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnBeforeExecuteAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 任务执行后的钩子方法
    /// </summary>
    /// <param name="result">任务执行结果</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步任务</returns>
    Task OnAfterExecuteAsync(TaskResult result, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}