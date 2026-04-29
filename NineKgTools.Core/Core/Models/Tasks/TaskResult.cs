using System;
using System.Collections.Generic;

namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务执行结果
/// </summary>
public class TaskResult
{
    /// <summary>
    /// 任务ID
    /// </summary>
    public string TaskId { get; set; } = null!;
    
    /// <summary>
    /// 任务名称
    /// </summary>
    public string TaskName { get; set; } = null!;

    /// <summary>
    /// 任务类型
    /// </summary>
    public TaskType TaskType { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 结果消息
    /// </summary>
    public string? Message { get; set; }
    
    /// <summary>
    /// 错误信息（如果失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 异常详情（如果有）
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// 开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 结束时间
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// 执行耗时
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>
    /// 处理的项目数
    /// </summary>
    public int ProcessedItems { get; set; }
    
    /// <summary>
    /// 失败的项目数
    /// </summary>
    public int FailedItems { get; set; }

    /// <summary>
    /// 结果数据（可以存储任务特定的返回值）
    /// </summary>
    public Dictionary<string, object>? ResultData { get; set; }
    
    /// <summary>
    /// 创建成功结果
    /// </summary>
    public static TaskResult CreateSuccess(string taskId, string taskName, string? message = null, int processedItems = 0, TaskType taskType = TaskType.Custom)
    {
        return new TaskResult
        {
            TaskId = taskId,
            TaskName = taskName,
            TaskType = taskType,
            Success = true,
            Message = message ?? "任务执行成功",
            ProcessedItems = processedItems,
            EndTime = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 创建失败结果
    /// </summary>
    public static TaskResult CreateFailure(string taskId, string taskName, string errorMessage, Exception? exception = null, int failedCount = 0, TaskType taskType = TaskType.Custom)
    {
        return new TaskResult
        {
            TaskId = taskId,
            TaskName = taskName,
            TaskType = taskType,
            Success = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            FailedItems = failedCount,
            EndTime = DateTime.UtcNow
        };
    }
}