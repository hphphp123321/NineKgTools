using System;
using System.Collections.Generic;
using System.Threading;

namespace NineKgTools.Core.Models.Search;

/// <summary>
/// 全局搜索选项
/// </summary>
public class GlobalSearchOptions
{
    /// <summary>
    /// 搜索关键词
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否启用向量搜索
    /// </summary>
    public bool EnableVectorSearch { get; set; } = false;
    
    /// <summary>
    /// 搜索的实体类型
    /// </summary>
    public SearchEntityTypes EntityTypes { get; set; } = SearchEntityTypes.All;
    
    /// <summary>
    /// 分类过滤器
    /// </summary>
    public CategoryFilter? CategoryFilter { get; set; }
    
    /// <summary>
    /// 标签过滤器
    /// </summary>
    public TagFilter? TagFilter { get; set; }
    
    /// <summary>
    /// 评分过滤器
    /// </summary>
    public RatingFilter? RatingFilter { get; set; }
    
    /// <summary>
    /// 每种类型的最大结果数
    /// </summary>
    public int MaxResultsPerType { get; set; } = 20;
    
    /// <summary>
    /// 最小相关性分数阈值
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.3;
    
    /// <summary>
    /// 搜索取消令牌
    /// </summary>
    public CancellationToken CancellationToken { get; set; }
}

/// <summary>
/// 搜索实体类型枚举
/// </summary>
[Flags]
public enum SearchEntityTypes
{
    None = 0,
    Media = 1,
    Tag = 2,
    Circle = 4,
    Creator = 8,
    All = Media | Tag | Circle | Creator
}

/// <summary>
/// 分类过滤器
/// </summary>
public class CategoryFilter
{
    public List<int> CategoryIds { get; set; } = new();
    public FilterMode Mode { get; set; } = FilterMode.Union;
}

/// <summary>
/// 标签过滤器
/// </summary>
public class TagFilter
{
    public List<int> TagIds { get; set; } = new();
    public FilterMode Mode { get; set; } = FilterMode.Union;
}

/// <summary>
/// 评分过滤器
/// </summary>
public class RatingFilter
{
    public float MinRating { get; set; } = 0;
    public float MaxRating { get; set; } = 10;
}

/// <summary>
/// 过滤模式
/// </summary>
public enum FilterMode
{
    /// <summary>
    /// 并集模式 - 满足任一条件即可
    /// </summary>
    Union,
    
    /// <summary>
    /// 交集模式 - 必须满足所有条件
    /// </summary>
    Intersection
}