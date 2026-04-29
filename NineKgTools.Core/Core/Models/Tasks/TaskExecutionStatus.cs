namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务执行状态枚举
/// </summary>
public enum TaskExecutionStatus
{
    /// <summary>
    /// 等待执行
    /// </summary>
    Pending,
    
    /// <summary>
    /// 正在执行
    /// </summary>
    Running,
    
    /// <summary>
    /// 执行成功
    /// </summary>
    Succeeded,
    
    /// <summary>
    /// 执行失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// 已跳过
    /// </summary>
    Skipped,

    /// <summary>
    /// 正在重试
    /// </summary>
    Retrying,

    /// <summary>
    /// 超时
    /// </summary>
    Timeout,
    
    /// <summary>
    /// 未知状态
    /// </summary>
    Unknown
}