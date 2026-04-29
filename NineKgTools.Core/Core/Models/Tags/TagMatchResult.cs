namespace NineKgTools.Core.Models.Tags;

/// <summary>
/// 标签匹配结果
/// </summary>
public class TagMatchResult
{
    /// <summary>
    /// 匹配到的标签
    /// </summary>
    public Tag? Tag { get; set; }
    
    /// <summary>
    /// 匹配置信度（0-1之间）
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// 匹配类型
    /// </summary>
    public MatchType MatchType { get; set; }
    
    /// <summary>
    /// 原始查询字符串
    /// </summary>
    public string OriginalQuery { get; set; } = string.Empty;
    
    /// <summary>
    /// 匹配详情（可选，用于调试）
    /// </summary>
    public string? MatchDetails { get; set; }
}

/// <summary>
/// 匹配类型枚举
/// </summary>
public enum MatchType
{
    /// <summary>
    /// 用户自定义映射匹配（最高优先级）
    /// </summary>
    UserMapping,
    
    /// <summary>
    /// 精确匹配
    /// </summary>
    Exact,
    
    /// <summary>
    /// 规范化后的精确匹配（去除特殊字符、统一大小写）
    /// </summary>
    Normalized,
    
    /// <summary>
    /// 包含匹配（标签名互相包含）
    /// </summary>
    Contains,
    
    /// <summary>
    /// 相似度匹配（基于字符串相似度算法）
    /// </summary>
    Similarity,
    
    /// <summary>
    /// 分词匹配（中日文分词后匹配）
    /// </summary>
    Tokenized,
    
    /// <summary>
    /// 向量匹配（基于语义相似度）
    /// </summary>
    Vector,
    
    /// <summary>
    /// 无匹配
    /// </summary>
    None
}