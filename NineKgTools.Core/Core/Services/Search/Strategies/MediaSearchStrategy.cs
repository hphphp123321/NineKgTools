using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Search.Strategies;

/// <summary>
/// 媒体搜索策略
/// </summary>
public class MediaSearchStrategy : ISearchStrategy<MediaBase>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly VectorService? _vectorService;
    private readonly VectorEmbeddingService? _embeddingService;
    private readonly Config _config;
    private readonly SearchConfig _searchConfig;
    
    public MediaSearchStrategy(
        IServiceScopeFactory serviceScopeFactory,
        Config config,
        VectorService? vectorService = null,
        VectorEmbeddingService? embeddingService = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _config = config;
        _searchConfig = config.Search;
        _vectorService = vectorService;
        _embeddingService = embeddingService;
    }
    
    public async Task<List<SearchResultItem<MediaBase>>> SearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResultItem<MediaBase>>();
        
        var results = new List<SearchResultItem<MediaBase>>();
        
        try
        {
            // 1. 文本搜索
            var textResults = await PerformTextSearchAsync(query, options, cancellationToken);
            results.AddRange(textResults);
            
            // 2. 向量搜索（如果启用）
            if (options.EnableVectorSearch &&
                _config?.Ai?.UseAi == true &&
                _config?.Ai?.Vector?.Enable == true &&
                _config?.Ai?.Vector?.Media?.Enable == true &&
                _vectorService != null &&
                _embeddingService != null)
            {
                var vectorResults = await PerformVectorSearchAsync(query, options, cancellationToken);

                // 合并结果，如果同一个媒体同时出现在文本和向量搜索中，取较高分数
                foreach (var vectorResult in vectorResults)
                {
                    var existingResult = results.FirstOrDefault(r => r.Entity.Id == vectorResult.Entity.Id);
                    if (existingResult != null)
                    {
                        // 组合分数，使用配置的权重
                        existingResult.RelevanceScore = RelevanceScorer.CombineScores(
                            existingResult.RelevanceScore,
                            vectorResult.RelevanceScore,
                            _config?.Ai?.Vector?.Search?.Weight ?? 0.6
                        );
                    }
                    else
                    {
                        results.Add(vectorResult);
                    }
                }
            }
            
            // 3. 应用过滤器
            results = await ApplyFiltersAsync(results, options, cancellationToken);
            
            // 4. 过滤低于阈值的结果
            results = results.Where(r => r.RelevanceScore >= options.MinRelevanceScore).ToList();
            
            // 5. 排序并限制结果数量
            results = results
                .OrderByDescending(r => r.RelevanceScore)
                .Take(options.MaxResultsPerType)
                .ToList();
            
            // 6. 生成高亮文本（如果启用）
            if (_searchConfig.TextSearch.EnableHighlighting)
            {
                foreach (var result in results)
                {
                    GenerateHighlights(result, query);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Log.Debug("媒体搜索被取消: {Query}", query);
            throw;
        }
        catch (Exception ex) when (cancellationToken.IsCancellationRequested)
        {
            // 取消期间由于 DbContext/连接关闭引发的派生异常不是真正的错误，降级为 Debug
            Log.Debug(ex, "媒体搜索被取消（连接关闭）: {Query}", query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "媒体搜索失败: {Query}", query);
        }

        return results;
    }
    
    private async Task<List<SearchResultItem<MediaBase>>> PerformTextSearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultItem<MediaBase>>();
        var queryLower = query.ToLowerInvariant();
        
        // 使用新的作用域和 DbContext 实例
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        
        // 构建基础查询
        var mediaQuery = context.Medias
            .Include(m => m.Poster)
            .Include(m => m.Category)
            .Include(m => m.Tags)
            .Include(m => m.Circle)
            .AsNoTracking();
        
        // 获取所有媒体（这里可以优化为使用数据库全文搜索）
        var allMedia = await mediaQuery.ToListAsync(cancellationToken);
        
        foreach (var media in allMedia)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            double maxScore = 0;
            SearchMatchType matchType = SearchMatchType.Fuzzy;
            string matchField = "";
            
            // 搜索标题
            var titleScore = RelevanceScorer.CalculateTextRelevance(query, media.Title, "title");
            if (titleScore > maxScore)
            {
                maxScore = titleScore;
                matchType = GetMatchType(query, media.Title);
                matchField = "Title";
            }
            
            // 搜索别名
            foreach (var alias in media.AliasTitles)
            {
                var aliasScore = RelevanceScorer.CalculateTextRelevance(query, alias, "aliastitle");
                if (aliasScore > maxScore)
                {
                    maxScore = aliasScore;
                    matchType = SearchMatchType.Alias;
                    matchField = "AliasTitle";
                }
            }
            
            // 搜索简介
            if (!string.IsNullOrWhiteSpace(media.Summary))
            {
                var summaryScore = RelevanceScorer.CalculateTextRelevance(query, media.Summary, "summary");
                if (summaryScore > maxScore * 0.8) // 简介匹配权重略低
                {
                    maxScore = Math.Max(maxScore, summaryScore);
                    if (matchField == "")
                    {
                        matchType = SearchMatchType.Description;
                        matchField = "Summary";
                    }
                }
            }
            
            // 搜索描述
            if (!string.IsNullOrWhiteSpace(media.Description))
            {
                var descScore = RelevanceScorer.CalculateTextRelevance(query, media.Description, "description");
                if (descScore > maxScore * 0.7) // 描述匹配权重更低
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
                results.Add(new SearchResultItem<MediaBase>
                {
                    Entity = media,
                    RelevanceScore = maxScore,
                    MatchType = matchType,
                    MatchDetails = $"匹配字段: {matchField}"
                });
            }
        }
        
        return results;
    }
    
    private async Task<List<SearchResultItem<MediaBase>>> PerformVectorSearchAsync(
        string query,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var results = new List<SearchResultItem<MediaBase>>();
        
        try
        {
            // 生成查询向量
            var queryEmbedding = await _embeddingService!.GenerateEmbeddingAsync(query);
            
            // 搜索相似的媒体
            var vectorResults = await _vectorService!.SearchMediaAsync(
                queryEmbedding,
                topK: options.MaxResultsPerType * 2, // 获取更多结果以便过滤
                threshold: _config?.Ai?.Vector?.Media?.MinSimilarity ?? 0.7 // 使用配置的向量相似度阈值
            );
            
            if (!vectorResults.Any())
            {
                Log.Debug("媒体向量搜索未找到结果: {Query}", query);
                return results;
            }
            
            // 获取媒体ID列表
            var mediaIds = vectorResults
                .Where(r => r.Record != null)
                .Select(r => r.Record!.MediaId)
                .Distinct()
                .ToList();
            
            // 使用新的作用域和 DbContext 实例
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            // 批量加载媒体实体
            var mediaDict = await context.Medias
                .Include(m => m.Category)
                .Include(m => m.Tags)
                .Include(m => m.Circle)
                .Where(m => mediaIds.Contains(m.Id))
                .AsNoTracking()
                .ToDictionaryAsync(m => m.Id, cancellationToken);
            
            // 构建搜索结果
            foreach (var vectorResult in vectorResults)
            {
                if (vectorResult.Record == null)
                    continue;
                
                if (mediaDict.TryGetValue(vectorResult.Record.MediaId, out var media))
                {
                    results.Add(new SearchResultItem<MediaBase>
                    {
                        Entity = media,
                        RelevanceScore = vectorResult.Score,
                        MatchType = SearchMatchType.Vector,
                        MatchDetails = $"向量相似度: {vectorResult.Score:F3}"
                    });
                }
            }
            
            Log.Debug("媒体向量搜索找到 {Count} 个结果", results.Count);
        }
        catch (Exception ex)
        {
            if (ex is TaskCanceledException)
            {
                throw;
            }

            Log.Error(ex, "媒体向量搜索失败: {Query}", query);
        }
        
        return results;
    }
    
    private async Task<List<SearchResultItem<MediaBase>>> ApplyFiltersAsync(
        List<SearchResultItem<MediaBase>> results,
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        // 分类过滤
        if (options.CategoryFilter != null && options.CategoryFilter.CategoryIds.Any())
        {
            if (options.CategoryFilter.Mode == FilterMode.Union)
            {
                // 并集：包含任一分类
                results = results.Where(r =>
                    options.CategoryFilter.CategoryIds.Contains(r.Entity.Category?.Id ?? 0)
                ).ToList();
            }
            else
            {
                // 交集：这里不适用，因为每个媒体只有一个分类
                results = results.Where(r =>
                    options.CategoryFilter.CategoryIds.Contains(r.Entity.Category?.Id ?? 0)
                ).ToList();
            }
        }
        
        // 标签过滤
        if (options.TagFilter != null && options.TagFilter.TagIds.Any())
        {
            if (options.TagFilter.Mode == FilterMode.Union)
            {
                // 并集：包含任一标签
                results = results.Where(r =>
                    r.Entity.Tags.Any(t => options.TagFilter.TagIds.Contains(t.Id))
                ).ToList();
            }
            else
            {
                // 交集：包含所有标签
                results = results.Where(r =>
                    options.TagFilter.TagIds.All(tagId =>
                        r.Entity.Tags.Any(t => t.Id == tagId))
                ).ToList();
            }
        }
        
        // 评分过滤
        if (options.RatingFilter != null)
        {
            results = results.Where(r =>
                r.Entity.Rating >= options.RatingFilter.MinRating &&
                r.Entity.Rating <= options.RatingFilter.MaxRating
            ).ToList();
        }
        
        return await Task.FromResult(results);
    }
    
    private void GenerateHighlights(SearchResultItem<MediaBase> result, string query)
    {
        var media = result.Entity;
        var highlightTag = _searchConfig.TextSearch.HighlightTag;
        
        // 标题高亮
        if (media.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Title",
                OriginalText = media.Title,
                HighlightedText = RelevanceScorer.HighlightText(media.Title, query, highlightTag)
            });
        }
        
        // 简介高亮
        if (!string.IsNullOrWhiteSpace(media.Summary) && 
            media.Summary.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            var snippet = RelevanceScorer.GetTextSnippet(media.Summary, query);
            result.Highlights.Add(new HighlightSnippet
            {
                FieldName = "Summary",
                OriginalText = snippet,
                HighlightedText = RelevanceScorer.HighlightText(snippet, query, highlightTag)
            });
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