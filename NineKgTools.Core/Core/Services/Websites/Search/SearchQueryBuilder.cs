using System.Web;
using NineKgTools.Utils;

namespace NineKgTools.Core.Services.Websites.Search;

/// <summary>
/// 搜索查询构建器，用于为不同网站构建适合的搜索URL
/// </summary>
public class SearchQueryBuilder
{
    private readonly ISearchStrategy _searchStrategy;
    
    public SearchQueryBuilder(ISearchStrategy? searchStrategy = null)
    {
        _searchStrategy = searchStrategy ?? new MultiKeywordSearchStrategy();
    }
    
    /// <summary>
    /// 为DLsite构建搜索URL
    /// </summary>
    public List<string> BuildDLsiteSearchUrls(MediaKeywords keywords, string siteType = "maniax", string locale = "zh-CN")
    {
        var urls = new List<string>();
        var queries = _searchStrategy.GenerateSearchQueries(keywords, " ");
        
        foreach (var query in queries)
        {
            // URL编码查询字符串
            var encodedQuery = HttpUtility.UrlEncode(query.Query);
            
            var baseUrl = $"https://www.dlsite.com/{siteType}/fsr/=/keyword/{encodedQuery}" +
                         "/trend/options_and_or/and/per_page/30/page/1/from/fs.header";
            
            // 添加语言后缀
            var urlWithLocale = $"{baseUrl}?locale={locale}";
            
            urls.Add(urlWithLocale);
        }
        
        return urls;
    }
    
    /// <summary>
    /// 为Bangumi构建搜索查询
    /// </summary>
    /// <param name="keywords">关键词信息</param>
    /// <returns>搜索查询列表</returns>
    public List<SearchQuery> BuildBangumiSearchQueries(MediaKeywords keywords)
    {
        // Bangumi API 不支持多关键词用空格分隔，需要特殊处理
        // 使用较短的查询避免API限制
        var queries = _searchStrategy.GenerateSearchQueries(keywords, " ");
        
        // 过滤掉过长的查询（Bangumi API 对查询长度有限制）
        var filteredQueries = new List<SearchQuery>();
        
        foreach (var query in queries)
        {
            // Bangumi 搜索不太支持空格分隔的多关键词，优先使用单个关键词或短查询
            if (query.Type == SearchQueryType.ProductCode || 
                query.Type == SearchQueryType.SingleKeyword ||
                query.Type == SearchQueryType.CircleName)
            {
                filteredQueries.Add(query);
            }
            else if (query.Query.Length <= 20) // 限制查询长度
            {
                // 对于多关键词查询，尝试不同的组合方式
                var modifiedQuery = new SearchQuery
                {
                    Query = query.Query.Replace(" ", ""), // 移除空格
                    Type = query.Type,
                    Priority = query.Priority,
                    OriginalKeywords = query.OriginalKeywords
                };
                filteredQueries.Add(modifiedQuery);
            }
        }
        
        return filteredQueries;
    }
    
    /// <summary>
    /// 为通用网站构建搜索查询
    /// </summary>
    /// <param name="keywords">关键词信息</param>
    /// <param name="separator">关键词分隔符</param>
    /// <returns>搜索查询列表</returns>
    public List<SearchQuery> BuildGenericSearchQueries(MediaKeywords keywords, string separator = " ")
    {
        return _searchStrategy.GenerateSearchQueries(keywords, separator);
    }
}