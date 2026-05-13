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
/// 创作者搜索策略 —— 仅支持 Exact / StartsWith / Contains 三档相关度。
/// **不走 Fuzzy 编辑距离**：创作者名字 / 别名都是用户可识别的专名，模糊匹配只会产生
/// "为什么这个也搜出来了"的困惑结果。query 不真正出现在某字段中则该项不命中。
/// </summary>
public class CreatorSearchStrategy : ISearchStrategy<Creator>
{
    private readonly MediaDbContext _context;

    public CreatorSearchStrategy(MediaDbContext context)
    {
        _context = context;
    }

    public async Task<List<SearchResultItem<Creator>>> SearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResultItem<Creator>>();

        var results = new List<SearchResultItem<Creator>>();

        try
        {
            var allCreators = await _context.Creators
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var creator in allCreators)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double maxScore = 0;
                // 命中时由 GetMatchType 精化到 Exact / Contains —— 不可能 Fuzzy
                SearchMatchType matchType = SearchMatchType.Contains;
                string matchField = "";

                // 1. Name（只 Exact / StartsWith / Contains，不 Fuzzy）
                var nameScore = RelevanceScorer.CalculateContainsRelevance(query, creator.Name, "name");
                if (nameScore > maxScore)
                {
                    maxScore = nameScore;
                    matchType = GetMatchType(query, creator.Name);
                    matchField = "Name";
                }

                // 2. AliasNames（所有别名取最高分）
                if (creator.AliasNames is { Count: > 0 })
                {
                    foreach (var alias in creator.AliasNames)
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
                if (!string.IsNullOrWhiteSpace(creator.Description))
                {
                    var descScore = RelevanceScorer.CalculateContainsRelevance(query, creator.Description, "description");
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
                    var resultItem = new SearchResultItem<Creator>
                    {
                        Entity = creator,
                        RelevanceScore = maxScore,
                        MatchType = matchType,
                        MatchDetails = $"匹配字段: {matchField} | 类型: {GetCreatorTypeString(creator.Types)}"
                    };
                    GenerateHighlights(resultItem, query, matchField);
                    results.Add(resultItem);
                }
            }

            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .ThenBy(r => r.Entity.Types.FirstOrDefault())
                .Take(options.MaxResultsPerType)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            Log.Debug("创作者搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug(ex, "创作者搜索被取消（连接关闭）: {Query}", query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创作者搜索失败: {Query}", query);
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

    private static void GenerateHighlights(SearchResultItem<Creator> result, string query, string matchField)
    {
        var creator = result.Entity;
        switch (matchField)
        {
            case "Name":
                result.Highlights.Add(new HighlightSnippet
                {
                    FieldName = "Name",
                    OriginalText = creator.Name,
                    HighlightedText = RelevanceScorer.HighlightText(creator.Name, query)
                });
                break;
            case "AliasName":
                var alias = creator.AliasNames.FirstOrDefault(a =>
                    !string.IsNullOrEmpty(a) && a.Contains(query, StringComparison.OrdinalIgnoreCase))
                    ?? creator.AliasNames.FirstOrDefault();
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
            case "Description" when !string.IsNullOrWhiteSpace(creator.Description):
                result.Highlights.Add(new HighlightSnippet
                {
                    FieldName = "Description",
                    OriginalText = creator.Description!,
                    HighlightedText = RelevanceScorer.HighlightText(creator.Description!, query)
                });
                break;
        }
    }

    private static string GetCreatorTypeString(List<CreatorType> types)
    {
        if (types is null || types.Count == 0) return "未知";
        var typeNames = types.Select(t => t switch
        {
            CreatorType.Author => "作者",
            CreatorType.Illustrator => "画师",
            CreatorType.Musician => "音乐",
            CreatorType.ScreenWriter => "编剧",
            CreatorType.VoiceActor => "声优",
            CreatorType.Director => "导演",
            CreatorType.Actor => "演员",
            _ => t.ToString()
        });
        return string.Join(", ", typeNames);
    }
}
