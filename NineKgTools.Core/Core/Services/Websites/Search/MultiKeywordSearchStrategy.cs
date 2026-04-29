using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Search;

/// <summary>
/// 多关键词搜索策略，生成多种关键词组合进行搜索
/// </summary>
public class MultiKeywordSearchStrategy : ISearchStrategy
{
    /// <summary>
    /// 生成搜索查询列表
    /// </summary>
    public List<SearchQuery> GenerateSearchQueries(MediaKeywords keywords, string separator = " ")
    {
        var queries = new List<SearchQuery>();
        
        // 1. 如果有产品代码，最高优先级
        if (!string.IsNullOrEmpty(keywords.ProductCode))
        {
            queries.Add(new SearchQuery
            {
                Query = keywords.ProductCode,
                Type = SearchQueryType.ProductCode,
                Priority = 100,
                OriginalKeywords = new List<string> { keywords.ProductCode }
            });
            
            Log.Debug("生成产品代码搜索查询: {Query}", keywords.ProductCode);
        }
        
        // 2. 尝试完整的清理后标题（中等优先级）
        if (!string.IsNullOrEmpty(keywords.CleanedTitle) && keywords.CleanedTitle.Length <= 50)
        {
            queries.Add(new SearchQuery
            {
                Query = keywords.CleanedTitle,
                Type = SearchQueryType.FullTitle,
                Priority = 80,
                OriginalKeywords = ExtractAllKeywords(keywords)
            });
            
            Log.Debug("生成完整标题搜索查询: {Query}", keywords.CleanedTitle);
        }
        
        // 3. 生成多关键词组合查询
        var allKeywords = keywords.GetAllKeywords();
        if (allKeywords.Count > 0)
        {
            // 3.1 主关键词 + 社团名组合（如果有）
            if (!string.IsNullOrEmpty(keywords.CircleName) && !string.IsNullOrEmpty(keywords.PrimaryKeyword))
            {
                var circleQuery = $"{keywords.CircleName}{separator}{keywords.PrimaryKeyword}";
                queries.Add(new SearchQuery
                {
                    Query = circleQuery,
                    Type = SearchQueryType.MultiKeyword,
                    Priority = 70,
                    OriginalKeywords = new List<string> { keywords.CircleName, keywords.PrimaryKeyword }
                });
                
                Log.Debug("生成社团+主关键词组合查询: {Query}", circleQuery);
            }
            
            // 3.2 主关键词 + 第一个次要关键词
            if (keywords.SecondaryKeywords.Count > 0 && !string.IsNullOrEmpty(keywords.PrimaryKeyword))
            {
                var primarySecondaryQuery = $"{keywords.PrimaryKeyword}{separator}{keywords.SecondaryKeywords[0]}";
                queries.Add(new SearchQuery
                {
                    Query = primarySecondaryQuery,
                    Type = SearchQueryType.MultiKeyword,
                    Priority = 60,
                    OriginalKeywords = new List<string> { keywords.PrimaryKeyword, keywords.SecondaryKeywords[0] }
                });
                
                Log.Debug("生成主+次关键词组合查询: {Query}", primarySecondaryQuery);
            }
            
            // 3.3 最多使用前3个关键词的组合
            if (allKeywords.Count >= 2)
            {
                var keywordsToUse = allKeywords.Take(Math.Min(3, allKeywords.Count)).ToList();
                var multiKeywordQuery = string.Join(separator, keywordsToUse);
                
                // 避免重复
                if (!queries.Any(q => q.Query == multiKeywordQuery))
                {
                    queries.Add(new SearchQuery
                    {
                        Query = multiKeywordQuery,
                        Type = SearchQueryType.MultiKeyword,
                        Priority = 50,
                        OriginalKeywords = keywordsToUse
                    });
                    
                    Log.Debug("生成多关键词组合查询: {Query}", multiKeywordQuery);
                }
            }
        }
        
        // 4. 单关键词查询作为后备
        if (!string.IsNullOrEmpty(keywords.PrimaryKeyword))
        {
            queries.Add(new SearchQuery
            {
                Query = keywords.PrimaryKeyword,
                Type = SearchQueryType.SingleKeyword,
                Priority = 40,
                OriginalKeywords = new List<string> { keywords.PrimaryKeyword }
            });
            
            Log.Debug("生成主关键词查询: {Query}", keywords.PrimaryKeyword);
        }
        
        // 5. 社团名单独搜索（低优先级）
        if (!string.IsNullOrEmpty(keywords.CircleName))
        {
            queries.Add(new SearchQuery
            {
                Query = keywords.CircleName,
                Type = SearchQueryType.CircleName,
                Priority = 30,
                OriginalKeywords = new List<string> { keywords.CircleName }
            });
            
            Log.Debug("生成社团名查询: {Query}", keywords.CircleName);
        }
        
        // 6. 次要关键词单独搜索（最低优先级）
        foreach (var secondaryKeyword in keywords.SecondaryKeywords.Take(2))
        {
            queries.Add(new SearchQuery
            {
                Query = secondaryKeyword,
                Type = SearchQueryType.SingleKeyword,
                Priority = 20,
                OriginalKeywords = new List<string> { secondaryKeyword }
            });
            
            Log.Debug("生成次要关键词查询: {Query}", secondaryKeyword);
        }
        
        // 按优先级排序并去重
        return queries
            .GroupBy(q => q.Query)
            .Select(g => g.OrderByDescending(q => q.Priority).First())
            .OrderByDescending(q => q.Priority)
            .ToList();
    }
    
    /// <summary>
    /// 提取所有关键词
    /// </summary>
    private List<string> ExtractAllKeywords(MediaKeywords keywords)
    {
        var allKeywords = new List<string>();
        
        if (!string.IsNullOrEmpty(keywords.PrimaryKeyword))
            allKeywords.Add(keywords.PrimaryKeyword);
            
        allKeywords.AddRange(keywords.SecondaryKeywords);
        
        if (!string.IsNullOrEmpty(keywords.CircleName))
            allKeywords.Add(keywords.CircleName);
            
        return allKeywords.Distinct().ToList();
    }
}