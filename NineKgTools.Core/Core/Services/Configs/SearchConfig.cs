using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 搜索配置
/// </summary>
public class SearchConfig
{
    /// <summary>
    /// 是否启用全局搜索
    /// </summary>
    [YamlMember(Alias = "enable_global_search", Description = "是否启用全局搜索功能")]
    public bool EnableGlobalSearch { get; set; } = true;

    /// <summary>
    /// 是否启用搜索缓存
    /// </summary>
    [YamlMember(Alias = "enable_search_cache", Description = "是否启用搜索缓存以提高性能")]
    public bool EnableSearchCache { get; set; } = true;

    /// <summary>
    /// 缓存过期时间（分钟）
    /// </summary>
    [YamlMember(Alias = "cache_expiration_minutes", Description = "搜索缓存过期时间（分钟）")]
    public int CacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// 最大并发搜索数
    /// </summary>
    [YamlMember(Alias = "max_concurrent_searches", Description = "最大并发搜索数量限制")]
    public int MaxConcurrentSearches { get; set; } = 10;

    /// <summary>
    /// 每种类型默认最大结果数
    /// </summary>
    [YamlMember(Alias = "default_max_results_per_type", Description = "每种类型默认最大结果数")]
    public int DefaultMaxResultsPerType { get; set; } = 20;

    /// <summary>
    /// 默认最小相关性分数
    /// </summary>
    [YamlMember(Alias = "default_min_relevance_score", Description = "默认最小相关性分数（低于此分数的结果将被过滤）")]
    public double DefaultMinRelevanceScore { get; set; } = 0.3;

    /// <summary>
    /// 搜索超时时间（秒）
    /// </summary>
    [YamlMember(Alias = "search_timeout_seconds", Description = "搜索超时时间（秒）")]
    public int SearchTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 文本搜索配置
    /// </summary>
    [YamlMember(Alias = "text_search", Description = "文本搜索相关配置")]
    public TextSearchConfig TextSearch { get; set; } = new();

    /// <summary>
    /// 创建配置副本
    /// </summary>
    public SearchConfig Copy()
    {
        return new SearchConfig
        {
            EnableGlobalSearch = EnableGlobalSearch,
            EnableSearchCache = EnableSearchCache,
            CacheExpirationMinutes = CacheExpirationMinutes,
            MaxConcurrentSearches = MaxConcurrentSearches,
            DefaultMaxResultsPerType = DefaultMaxResultsPerType,
            DefaultMinRelevanceScore = DefaultMinRelevanceScore,
            SearchTimeoutSeconds = SearchTimeoutSeconds,
            TextSearch = TextSearch.Copy()
        };
    }
}

/// <summary>
/// 文本搜索配置
/// </summary>
public class TextSearchConfig
{
    /// <summary>
    /// 是否启用高亮
    /// </summary>
    [YamlMember(Alias = "enable_highlighting", Description = "是否启用搜索结果高亮显示")]
    public bool EnableHighlighting { get; set; } = true;

    /// <summary>
    /// 高亮标签
    /// </summary>
    [YamlMember(Alias = "highlight_tag", Description = "搜索结果高亮显示使用的HTML标签")]
    public string HighlightTag { get; set; } = "<mark>";

    /// <summary>
    /// 创建配置副本
    /// </summary>
    public TextSearchConfig Copy()
    {
        return new TextSearchConfig
        {
            EnableHighlighting = EnableHighlighting,
            HighlightTag = HighlightTag
        };
    }
}
