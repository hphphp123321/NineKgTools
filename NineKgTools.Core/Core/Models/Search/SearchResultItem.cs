using System.Collections.Generic;

namespace NineKgTools.Core.Models.Search;

/// <summary>
/// 搜索结果项
/// </summary>
public class SearchResultItem<T>
{
    /// <summary>
    /// 搜索到的实体
    /// </summary>
    public T Entity { get; set; } = default!;
    
    /// <summary>
    /// 相关性分数 (0-1)
    /// </summary>
    public double RelevanceScore { get; set; }
    
    /// <summary>
    /// 匹配类型
    /// </summary>
    public SearchMatchType MatchType { get; set; }
    
    /// <summary>
    /// 匹配详情
    /// </summary>
    public string? MatchDetails { get; set; }
    
    /// <summary>
    /// 高亮文本片段
    /// </summary>
    public List<HighlightSnippet> Highlights { get; set; } = new();
}

/// <summary>
/// 搜索匹配类型
/// </summary>
public enum SearchMatchType
{
    /// <summary>
    /// 精确匹配
    /// </summary>
    Exact,
    
    /// <summary>
    /// 模糊匹配
    /// </summary>
    Fuzzy,
    
    /// <summary>
    /// 包含匹配
    /// </summary>
    Contains,
    
    /// <summary>
    /// 向量语义匹配
    /// </summary>
    Vector,
    
    /// <summary>
    /// 别名匹配
    /// </summary>
    Alias,
    
    /// <summary>
    /// 描述匹配
    /// </summary>
    Description
}

/// <summary>
/// 高亮文本片段
/// </summary>
public class HighlightSnippet
{
    /// <summary>
    /// 字段名
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// 高亮文本（包含HTML标记）
    /// </summary>
    public string HighlightedText { get; set; } = string.Empty;
    
    /// <summary>
    /// 原始文本
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;
}