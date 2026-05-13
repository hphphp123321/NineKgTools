using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Vectors;
using NineKgTools.Core.Services.Search;
using NineKgTools.Core.Services.Configs;
using Serilog;
using TagMatchType = NineKgTools.Core.Models.Tags.MatchType;

namespace NineKgTools.Core.Services.Search.Strategies;

/// <summary>
/// 标签搜索策略
/// </summary>
public class TagSearchStrategy : ISearchStrategy<Tag>
{
    private readonly MediaDbContext _context;
    private readonly TagMatchingService _tagMatchingService;
    private readonly VectorService? _vectorService;
    private readonly VectorEmbeddingService? _embeddingService;
    private readonly Config _config;
    private readonly SearchConfig _searchConfig;

    public TagSearchStrategy(
        MediaDbContext context,
        TagMatchingService tagMatchingService,
        Config config,
        VectorService? vectorService = null,
        VectorEmbeddingService? embeddingService = null,
        SearchConfig? searchConfig = null)
    {
        _context = context;
        _tagMatchingService = tagMatchingService;
        _config = config;
        _vectorService = vectorService;
        _embeddingService = embeddingService;
        _searchConfig = searchConfig ?? new SearchConfig();
    }
    
    public async Task<List<SearchResultItem<Tag>>> SearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResultItem<Tag>>();
        
        var results = new List<SearchResultItem<Tag>>();
        
        try
        {
            // 1. 使用 TagMatchingService 进行智能匹配（包含向量搜索）
            if (options.EnableVectorSearch &&
                _config?.Ai?.UseAi == true &&
                _config?.Ai?.Vector?.Enable == true &&
                _config?.Ai?.Vector?.Tag?.Enable == true)
            {
                var matchResult = await _tagMatchingService.FindBestMatchAsync(query);
                if (matchResult?.Tag != null)
                {
                    results.Add(new SearchResultItem<Tag>
                    {
                        Entity = matchResult.Tag,
                        RelevanceScore = matchResult.Confidence,
                        MatchType = ConvertMatchType(matchResult.MatchType),
                        MatchDetails = matchResult.MatchDetails
                    });
                }
                
                // 获取多个匹配结果
                var multipleResults = await _tagMatchingService.FindAllMatchesAsync(query);
                foreach (var result in multipleResults)
                {
                    if (result.Tag != null && !results.Any(r => r.Entity.Id == result.Tag.Id))
                    {
                        results.Add(new SearchResultItem<Tag>
                        {
                            Entity = result.Tag,
                            RelevanceScore = result.Confidence,
                            MatchType = ConvertMatchType(result.MatchType),
                            MatchDetails = result.MatchDetails
                        });
                    }
                }
            }
            
            // 2. 文本搜索
            var textResults = await PerformTextSearchAsync(query, options, cancellationToken);
            
            // 合并结果，避免重复
            foreach (var textResult in textResults)
            {
                if (!results.Any(r => r.Entity.Id == textResult.Entity.Id))
                {
                    results.Add(textResult);
                }
                else
                {
                    // 如果已存在，更新分数（取较高值）
                    var existing = results.First(r => r.Entity.Id == textResult.Entity.Id);
                    if (textResult.RelevanceScore > existing.RelevanceScore)
                    {
                        existing.RelevanceScore = textResult.RelevanceScore;
                        existing.MatchType = textResult.MatchType;
                    }
                }
            }
            
            // 3. 过滤低于阈值的结果
            results = results.Where(r => r.RelevanceScore >= options.MinRelevanceScore).ToList();
            
            // 4. 排序并限制结果数量
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(options.MaxResultsPerType)
                .ToList();
            
            // 5. 生成高亮文本
            foreach (var result in results)
            {
                GenerateHighlights(result, query);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("标签搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // 取消期间由于 DbContext/连接关闭引发的派生异常（如 SqliteException "An error occurred using the connection"）
            // 不是真正的错误，降级为 Debug，避免搜索抖动时刷屏 [EROR]。
            Log.Debug(ex, "标签搜索被取消（连接关闭）: {Query}", query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "标签搜索失败: {Query}", query);
        }

        return results;
    }
    
    private async Task<List<SearchResultItem<Tag>>> PerformTextSearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultItem<Tag>>();
        var queryLower = query.ToLowerInvariant();
        
        // 获取所有标签
        var allTags = await _context.Tags
            .Include(t => t.TopTag)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        
        foreach (var tag in allTags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double maxScore = 0;
            // 关键词搜索默认 Contains（不走 Fuzzy 编辑距离）—— 命中时由 GetMatchType 精化到 Exact / Contains
            SearchMatchType matchType = SearchMatchType.Contains;
            string matchField = "";

            // 搜索标签名（只 Exact / StartsWith / Contains，不 Fuzzy）
            var nameScore = RelevanceScorer.CalculateContainsRelevance(query, tag.Name, "name");
            if (nameScore > maxScore)
            {
                maxScore = nameScore;
                matchType = GetMatchType(query, tag.Name);
                matchField = "Name";
            }

            // 搜索描述（权重较低）
            if (!string.IsNullOrWhiteSpace(tag.Description))
            {
                var descScore = RelevanceScorer.CalculateContainsRelevance(query, tag.Description, "description");
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
            
            // 如果有匹配，添加到结果
            if (maxScore > 0)
            {
                results.Add(new SearchResultItem<Tag>
                {
                    Entity = tag,
                    RelevanceScore = maxScore,
                    MatchType = matchType,
                    MatchDetails = $"匹配字段: {matchField}"
                });
            }
        }
        
        return results;
    }
    
    private void GenerateHighlights(SearchResultItem<Tag> result, string query)
    {
        var tag = result.Entity;
        
        // 名称高亮
        if (tag.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Name",
                OriginalText = tag.Name,
                HighlightedText = RelevanceScorer.HighlightText(tag.Name, query)
            });
        }
        
        // 描述高亮
        if (!string.IsNullOrWhiteSpace(tag.Description) && 
            tag.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            var snippet = RelevanceScorer.GetTextSnippet(tag.Description, query);
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Description",
                OriginalText = snippet,
                HighlightedText = RelevanceScorer.HighlightText(snippet, query)
            });
        }
    }
    
    private SearchMatchType GetMatchType(string query, string text)
    {
        var queryLower = query.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        // 关键词搜索只可能 Exact 或 Contains —— Fuzzy 路径已删除
        if (textLower.Equals(queryLower))
            return SearchMatchType.Exact;
        return SearchMatchType.Contains;
    }
    
    private SearchMatchType ConvertMatchType(TagMatchType matchType)
    {
        return matchType switch
        {
            TagMatchType.Exact => SearchMatchType.Exact,
            TagMatchType.UserMapping => SearchMatchType.Exact,
            TagMatchType.Normalized => SearchMatchType.Fuzzy,
            TagMatchType.Contains => SearchMatchType.Contains,
            TagMatchType.Similarity => SearchMatchType.Fuzzy,
            TagMatchType.Vector => SearchMatchType.Vector,
            _ => SearchMatchType.Fuzzy
        };
    }
}