using NineKgTools.Utils;

namespace NineKgTools.Core.Services.Websites.Search;

/// <summary>
/// 搜索策略接口，定义不同的搜索行为
/// </summary>
public interface ISearchStrategy
{
    /// <summary>
    /// 根据提取的关键词生成搜索查询
    /// </summary>
    /// <param name="keywords">从文件名提取的关键词信息</param>
    /// <param name="separator">关键词分隔符（如空格、|等）</param>
    /// <returns>搜索查询列表，按优先级排序</returns>
    List<SearchQuery> GenerateSearchQueries(MediaKeywords keywords, string separator = " ");
}

/// <summary>
/// 搜索查询结构
/// </summary>
public class SearchQuery
{
    /// <summary>
    /// 搜索查询字符串
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// 查询类型（用于区分不同的搜索策略）
    /// </summary>
    public SearchQueryType Type { get; set; }
    
    /// <summary>
    /// 查询优先级（越高越优先）
    /// </summary>
    public int Priority { get; set; }
    
    /// <summary>
    /// 原始关键词列表（用于后续相关性评分）
    /// </summary>
    public List<string> OriginalKeywords { get; set; } = new();
}

/// <summary>
/// 搜索查询类型
/// </summary>
public enum SearchQueryType
{
    /// <summary>
    /// 产品代码精确搜索
    /// </summary>
    ProductCode,
    
    /// <summary>
    /// 完整标题搜索
    /// </summary>
    FullTitle,
    
    /// <summary>
    /// 多关键词组合搜索
    /// </summary>
    MultiKeyword,
    
    /// <summary>
    /// 单关键词搜索
    /// </summary>
    SingleKeyword,
    
    /// <summary>
    /// 社团名搜索
    /// </summary>
    CircleName
}