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
/// 创作者搜索策略
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
            // 获取所有创作者
            var allCreators = await _context.Creators
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var creator in allCreators)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double maxScore = 0;
                SearchMatchType matchType = SearchMatchType.Fuzzy;
                string matchField = "";

                // 精确匹配名称
                if (creator.Name.Equals(query, StringComparison.OrdinalIgnoreCase))
                {
                    maxScore = 1.0;
                    matchType = SearchMatchType.Exact;
                    matchField = "Name";
                }
                // 精确匹配别名
                else if (creator.AliasNames.Any(alias =>
                             alias.Equals(query, StringComparison.OrdinalIgnoreCase)))
                {
                    maxScore = 0.95;
                    matchType = SearchMatchType.Alias;
                    matchField = "AliasName";
                }


                // 如果有匹配且超过阈值，添加到结果
                if (maxScore >= options.MinRelevanceScore)
                {
                    var resultItem = new SearchResultItem<Creator>
                    {
                        Entity = creator,
                        RelevanceScore = maxScore,
                        MatchType = matchType,
                        MatchDetails = $"匹配字段: {matchField} | 类型: {GetCreatorTypeString(creator.Types)}"
                    };

                    // 生成高亮
                    GenerateHighlights(resultItem, query);

                    results.Add(resultItem);
                }
            }

            // 排序并限制结果数量
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .ThenBy(r => r.Entity.Types.FirstOrDefault()) // 按类型次要排序
                .Take(options.MaxResultsPerType)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            Log.Debug("创作者搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创作者搜索失败: {Query}", query);
        }

        return results;
    }

    private void GenerateHighlights(SearchResultItem<Creator> result, string query)
    {
        var creator = result.Entity;

        // 向量搜索模式下不生成高亮（因为只有精确匹配）
        if (result.MatchType == SearchMatchType.Exact)
        {
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Name",
                OriginalText = creator.Name,
                HighlightedText = creator.Name
            });
        }
        else if (result.MatchType == SearchMatchType.Alias)
        {
            var matchedAlias = creator.AliasNames.FirstOrDefault(a =>
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

    private string GetCreatorTypeString(List<CreatorType> types)
    {
        if (!types.Any())
            return "未知";

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