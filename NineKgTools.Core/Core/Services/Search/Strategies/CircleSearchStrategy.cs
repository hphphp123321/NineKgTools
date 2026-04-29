using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Search;
using Serilog;

namespace NineKgTools.Core.Services.Search.Strategies;

/// <summary>
/// 社团搜索策略
/// </summary>
public class CircleSearchStrategy : ISearchStrategy<Circle>
{
    private readonly MediaDbContext _context;

    public CircleSearchStrategy(MediaDbContext context)
    {
        _context = context;
    }

    public async Task<List<SearchResultItem<Circle>>> SearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResultItem<Circle>>();

        var results = new List<SearchResultItem<Circle>>();

        try
        {
            // 获取所有社团
            var allCircles = await _context.Circles
                .Include(c => c.Medias)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var circle in allCircles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double maxScore = 0;
                SearchMatchType matchType = SearchMatchType.Fuzzy;
                string matchField = "";

                // 精确匹配名称
                if (circle.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    maxScore = 1.0;
                    matchType = SearchMatchType.Exact;
                    matchField = "Name";
                }
                // 精确匹配别名
                else if (circle.AliasNames.Any(alias =>
                             alias.Equals(query, StringComparison.OrdinalIgnoreCase)))
                {
                    maxScore = 0.95;
                    matchType = SearchMatchType.Alias;
                    matchField = "AliasName";
                }


                // 如果有匹配且超过阈值，添加到结果
                if (maxScore >= options.MinRelevanceScore)
                {
                    var resultItem = new SearchResultItem<Circle>
                    {
                        Entity = circle,
                        RelevanceScore = maxScore,
                        MatchType = matchType,
                        MatchDetails = $"匹配字段: {matchField} | 作品数: {circle.Medias.Count}"
                    };

                    // 生成高亮
                    GenerateHighlights(resultItem, query);

                    results.Add(resultItem);
                }
            }

            // 排序并限制结果数量
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => r.Entity.Medias.Count) // 作品数量作为次要排序
                .Take(options.MaxResultsPerType)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            Log.Debug("社团搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "社团搜索失败: {Query}", query);
        }

        return results;
    }

    private void GenerateHighlights(SearchResultItem<Circle> result, string query)
    {
        var circle = result.Entity;

        // 不生成高亮（因为只有精确匹配）
        if (result.MatchType == SearchMatchType.Exact)
        {
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Name",
                OriginalText = circle.Name,
                HighlightedText = circle.Name
            });
        }
        else if (result.MatchType == SearchMatchType.Alias)
        {
            var matchedAlias = circle.AliasNames.FirstOrDefault(a =>
                a.Equals(query, StringComparison.OrdinalIgnoreCase));
            if (matchedAlias != null)
            {
                result.Highlights.Add(new HighlightSnippet
                {
                    FieldName = "AliasName",
                    OriginalText = matchedAlias,
                    HighlightedText = matchedAlias
                });
            }
        }
    }

    private SearchMatchType GetMatchType(string query, string text)
    {
        var queryLower = query.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        if (textLower.Equals(queryLower))
            return SearchMatchType.Exact;
        if (textLower.Contains(queryLower))
            return SearchMatchType.Contains;
        return SearchMatchType.Fuzzy;
    }
}