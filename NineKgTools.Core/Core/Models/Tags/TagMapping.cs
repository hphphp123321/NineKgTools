namespace NineKgTools.Core.Models.Tags;

/// <summary>
/// 标签映射实体，用于存储用户自定义的标签映射关系
/// </summary>
public class TagMapping
{
    /// <summary>
    /// 映射ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 源标签名称（用户输入的标签名）
    /// </summary>
    public string SourceName { get; set; } = string.Empty;
    
    /// <summary>
    /// 目标标签ID
    /// </summary>
    public int? TargetTagId { get; set; }
    
    /// <summary>
    /// 目标标签（导航属性）
    /// </summary>
    public Tag? TargetTag { get; set; }
    
    /// <summary>
    /// 是否启用此映射
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// 映射优先级（数值越小优先级越高）
    /// </summary>
    public int Priority { get; set; } = 100;
    
    /// <summary>
    /// 映射描述/说明
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
    
    /// <summary>
    /// 匹配次数（用于统计）
    /// </summary>
    public int HitCount { get; set; } = 0;
    
    /// <summary>
    /// 最后匹配时间
    /// </summary>
    public DateTime? LastHitAt { get; set; }
}