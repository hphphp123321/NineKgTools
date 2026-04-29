namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务优先级
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// 关键任务 - 最高优先级
    /// </summary>
    Critical = 0,
    
    /// <summary>
    /// 高优先级
    /// </summary>
    High = 1,
    
    /// <summary>
    /// 正常优先级
    /// </summary>
    Normal = 2,
    
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 3,
    
    /// <summary>
    /// 后台任务 - 最低优先级
    /// </summary>
    Background = 4
}