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
/// 社团搜索策略 —— 仅支持 Exact / StartsWith / Contains 三档相关度。
/// **不走 Fuzzy 编辑距离**：社团名 / 别名都是用户可识别的专名，模糊匹配只会产生
/// "为什么这个也搜出来了"的困惑结果。query 不真正出现在某字段中则该项不命中。
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
            var allCircles = await _context.Circles
                .Include(c => c.Medias)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var circle in allCircles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double maxScore = 0;
                // 命中时由 GetMatchType 精化到 Exact / Contains —— 不可能 Fuzzy
                SearchMatchType matchType = SearchMatchType.Contains;
                string matchField = "";

                // 1. Name（只 Exact / StartsWith / Contains，不 Fuzzy）
                var nameScore = RelevanceScorer.CalculateContainsRelevance(query, circle.Name, "name");
                if (nameScore > maxScore)
                {
                    maxScore = nameScore;
                    matchType = GetMatchType(query, circle.Name);
                    matchField = "Name";
                }

                // 2. AliasNames（所有别名取最高分）
                if (circle.AliasNames is { Count: > 0 })
                {
                    foreach (var alias in circle.AliasNames)
                    {
                        if (string.IsNullOrWhiteSpace(alias)) continue;
                        var aliasScore = RelevanceScorer.CalculateContainsRelevance(query, alias, "aliasname");
                        if (aliasScore > maxScore)
                        {
                            maxScore = aliasScore;
                            matchType = SearchMatchType.Alias;
                            matchField = "AliasName";
                        }
                    }
                }

                // 3. Description（权重较低，仅在主字段无命中时纳入）
                if (!string.IsNullOrWhiteSpace(circle.Description))
                {
                    var descScore = RelevanceScorer.CalculateContainsRelevance(query, circle.Description, "description");
                    if (descScore > maxScore * 0.7)
                    {
                        maxScore = Math.Max(maxScore, descScore);
                        if (matchField == "")
                        {
                            matchType = SearchMatchType.Description;
                            matchField = "Description";
                        }
                    }
                }

                if (maxScore >= options.MinRelevanceScore)
                {
                    var resultItem = new SearchResultItem<Circle>
                    {
                        Entity = circle,
                        RelevanceScore = maxScore,
                        MatchType = matchType,
                        MatchDetails = $"匹配字段: {matchField} | 作品数: {circle.Medias?.Count ?? 0}"
                    };
                    GenerateHighlights(resultItem, query, matchField);
                    results.Add(resultItem);
                }
            }

            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .ThenByDescending(r => r.Entity.Medias?.Count ?? 0)
                .Take(options.MaxResultsPerType)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            Log.Debug("社团搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug(ex, "社团搜索被取消（连接关闭）: {Query}", query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "社团搜索失败: {Query}", query);
        }

        return results;
    }

    private static SearchMatchType GetMatchType(string query, string text)
    {
        var q = query.ToLowerInvariant();
        var t = text.ToLowerInvariant();
        // 关键词搜索只可能 Exact 或 Contains —— Fuzzy 路径已删除
        if (t.Equals(q)) return SearchMatchType.Exact;
        return SearchMatchType.Contains;
    }

    private static void GenerateHighlights(SearchResultItem<Circle> result, string query, string matchField)
    {
        var circle = result.Entity;
        switch (matchField)
        {
            case "Name":
                result.Highlights.Add(new HighlightSnippet
                {
                    FieldName = "Name",
                    OriginalText = circle.Name,
                    HighlightedText = RelevanceScorer.HighlightText(circle.Name, query)
                });
                break;
            case "AliasName":
                var alias = circle.AliasNames.FirstOrDefault(a =>
                    !string.IsNullOrEmpty(a) && a.Contains(query, StringComparison.OrdinalIgnoreCase))
                    ?? circle.AliasNames.FirstOrDefault();
                if (alias is not null)
                {
                    result.Highlights.Add(new HighlightSnippet
                    {
                        FieldName = "AliasName",
                        OriginalText = alias,
                        HighlightedText = RelevanceScorer.HighlightText(alias, query)
                    });
                }
                break;
            case "Description" when !string.IsNullOrWhiteSpace(circle.Description):
                result.Highlights.Add(new HighlightSnippet
                {
                    FieldName = "Description",
                    OriginalText = circle.Description!,
                    HighlightedText = RelevanceScorer.HighlightText(circle.Description!, query)
                });
                break;
        }
    }
}
