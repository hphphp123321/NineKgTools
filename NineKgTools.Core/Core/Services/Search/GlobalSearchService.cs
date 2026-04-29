using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Search.Strategies;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Search;

/// <summary>
/// 全局搜索服务
/// </summary>
public class GlobalSearchService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly Config _config;
    private readonly SearchConfig _searchConfig;
    
    // 搜索管理器
    private readonly CancellableSearchManager _cancellableSearchManager;
    private readonly SearchCacheManager _cacheManager;
    
    // 向量服务（可选）
    private readonly VectorService? _vectorService;
    private readonly VectorEmbeddingService? _embeddingService;
    private readonly TagMatchingService _tagMatchingService;
    private readonly TagMappingService _tagMappingService;
    
    public GlobalSearchService(
        IServiceScopeFactory serviceScopeFactory,
        Config config,
        IMemoryCache memoryCache,
        TagMatchingService tagMatchingService,
        TagMappingService tagMappingService,
        VectorService? vectorService = null,
        VectorEmbeddingService? embeddingService = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _config = config;
        _searchConfig = config.Search ?? new SearchConfig();
        _vectorService = vectorService;
        _embeddingService = embeddingService;
        _tagMatchingService = tagMatchingService;
        _tagMappingService = tagMappingService;
        
        // 初始化管理器
        _cancellableSearchManager = new CancellableSearchManager(_searchConfig);
        _cacheManager = new SearchCacheManager(memoryCache, _searchConfig);
    }
    
    /// <summary>
    /// 执行全局搜索
    /// </summary>
    /// <param name="options">搜索选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果</returns>
    public async Task<GlobalSearchResult> SearchAsync(GlobalSearchOptions options, CancellationToken cancellationToken = default)
    {
        // 检查是否启用全局搜索
        if (!_searchConfig.EnableGlobalSearch)
        {
            return new GlobalSearchResult
            {
                Query = options.Query,
                ElapsedMilliseconds = 0,
                ErrorMessage = "全局搜索功能已禁用"
            };
        }

        // 验证输入
        if (string.IsNullOrWhiteSpace(options.Query))
        {
            return new GlobalSearchResult
            {
                Query = options.Query,
                ElapsedMilliseconds = 0,
                ErrorMessage = "搜索关键词不能为空"
            };
        }

        // 应用默认配置值
        if (options.MaxResultsPerType == 20) // 如果是默认值，使用配置的值
        {
            options.MaxResultsPerType = _searchConfig.DefaultMaxResultsPerType;
        }

        if (Math.Abs(options.MinRelevanceScore - 0.3) < 0.001) // 如果是默认值，使用配置的值
        {
            options.MinRelevanceScore = _searchConfig.DefaultMinRelevanceScore;
        }

        // 使用外部提供的 CancellationToken，或使用内部管理器
        return await _cancellableSearchManager.ExecuteSearchAsync(options, async (internalCancellationToken) =>
        {
            // 合并两个 CancellationToken：外部的和内部的
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, internalCancellationToken);
            var linkedToken = linkedCts.Token;

            var stopwatch = Stopwatch.StartNew();

            try
            {
                linkedToken.ThrowIfCancellationRequested();

                // 1. 检查缓存
                GlobalSearchResult? cachedResult = null;
                if (_searchConfig.EnableSearchCache)
                {
                    cachedResult = await _cacheManager.GetCachedResultAsync(options);
                    if (cachedResult != null)
                    {
                        Log.Debug("搜索命中缓存: {Query}", options.Query);
                        cachedResult.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        return cachedResult;
                    }
                }

                // 2. 执行搜索
                var result = await ExecuteSearchInternalAsync(options, linkedToken);

                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                // 3. 缓存结果（如果没有错误且启用了缓存）
                if (_searchConfig.EnableSearchCache &&
                    string.IsNullOrEmpty(result.ErrorMessage) &&
                    !result.WasCancelled)
                {
                    await _cacheManager.SetCachedResultAsync(options, result);
                }

                Log.Information("搜索完成: {Query}, 耗时: {ElapsedMs}ms, 结果数: {Count}",
                    options.Query, result.ElapsedMilliseconds, result.TotalCount);

                return result;
            }
            catch (OperationCanceledException)
            {
                Log.Debug("搜索被取消: {Query}", options.Query);
                return new GlobalSearchResult
                {
                    Query = options.Query,
                    WasCancelled = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "搜索失败: {Query}", options.Query);
                return new GlobalSearchResult
                {
                    Query = options.Query,
                    ErrorMessage = $"搜索失败: {ex.Message}",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
        });
    }
    
    /// <summary>
    /// 获取搜索建议
    /// </summary>
    public async Task<List<string>> GetSearchSuggestionsAsync(string query, int maxSuggestions = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<string>();
        
        var suggestions = new List<string>();
        
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var queryLower = query.ToLowerInvariant();
            
            // 从媒体标题获取建议
            var mediaTitles = await context.Medias
                .Where(m => m.Title.ToLower().Contains(queryLower))
                .Select(m => m.Title)
                .Take(maxSuggestions / 3)
                .AsNoTracking()
                .ToListAsync();
            suggestions.AddRange(mediaTitles);
            
            // 从标签名称获取建议
            var tagNames = await context.Tags
                .Where(t => t.Name.ToLower().Contains(queryLower))
                .Select(t => t.Name)
                .Take(maxSuggestions / 3)
                .AsNoTracking()
                .ToListAsync();
            suggestions.AddRange(tagNames);
            
            // 从社团名称获取建议
            var circleNames = await context.Circles
                .Where(c => c.Name.ToLower().Contains(queryLower))
                .Select(c => c.Name)
                .Take(maxSuggestions / 3)
                .AsNoTracking()
                .ToListAsync();
            suggestions.AddRange(circleNames);
            
            // 从创作者名称获取建议
            var creatorNames = await context.Creators
                .Where(c => c.Name.ToLower().Contains(queryLower))
                .Select(c => c.Name)
                .Take(maxSuggestions / 3)
                .AsNoTracking()
                .ToListAsync();
            suggestions.AddRange(creatorNames);
            
            // 去重并限制数量
            suggestions = suggestions
                .Distinct()
                .OrderBy(s => s.Length) // 优先显示较短的建议
                .ThenBy(s => s)
                .Take(maxSuggestions)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取搜索建议失败: {Query}", query);
        }
        
        return suggestions;
    }
    
    /// <summary>
    /// 预热搜索索引
    /// </summary>
    public async Task WarmupSearchIndexAsync()
    {
        try
        {
            Log.Information("开始预热搜索索引");
            
            using var scope = _serviceScopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            
            // 预加载常用数据到内存
            await context.Medias.Include(m => m.Tags).Take(100).LoadAsync();
            await context.Tags.Include(t => t.TopTag).LoadAsync();
            await context.Circles.Take(50).LoadAsync();
            await context.Creators.Take(50).LoadAsync();
            
            Log.Information("搜索索引预热完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "预热搜索索引失败");
        }
    }
    
    /// <summary>
    /// 清除搜索缓存
    /// </summary>
    public void ClearSearchCache()
    {
        _cacheManager.ClearAll();
        Log.Information("搜索缓存已清除");
    }
    
    /// <summary>
    /// 内部搜索执行方法
    /// </summary>
    private async Task<GlobalSearchResult> ExecuteSearchInternalAsync(
        GlobalSearchOptions options,
        CancellationToken cancellationToken)
    {
        var result = new GlobalSearchResult
        {
            Query = options.Query,
            UsedVectorSearch = options.EnableVectorSearch
        };
        
        // 并行执行各实体类型的搜索
        var tasks = new List<Task>();
        
        if (options.EntityTypes.HasFlag(SearchEntityTypes.Media))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 为每次搜索创建新的策略实例
                    var mediaSearchStrategy = new MediaSearchStrategy(_serviceScopeFactory, _config, _vectorService, _embeddingService);
                    result.MediaResults = await mediaSearchStrategy.SearchAsync(
                        options.Query, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                    {
                        Log.Error(ex, "媒体搜索任务失败");
                    }
                }
            }, cancellationToken));
        }
        
        if (options.EntityTypes.HasFlag(SearchEntityTypes.Tag))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 为每次搜索创建新的策略实例
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var tagSearchStrategy = new TagSearchStrategy(context, _tagMatchingService, _config, _vectorService, _embeddingService, _searchConfig);
                    result.TagResults = await tagSearchStrategy.SearchAsync(
                        options.Query, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (ex is not TaskCanceledException)
                        Log.Error(ex, "标签搜索任务失败");
                }
            }, cancellationToken));
        }
        
        if (options.EntityTypes.HasFlag(SearchEntityTypes.Circle))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 为每次搜索创建新的策略实例
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var circleSearchStrategy = new CircleSearchStrategy(context);
                    result.CircleResults = await circleSearchStrategy.SearchAsync(
                        options.Query, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "社团搜索任务失败");
                }
            }, cancellationToken));
        }
        
        if (options.EntityTypes.HasFlag(SearchEntityTypes.Creator))
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 为每次搜索创建新的策略实例
                    using var scope = _serviceScopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
                    var creatorSearchStrategy = new CreatorSearchStrategy(context);
                    result.CreatorResults = await creatorSearchStrategy.SearchAsync(
                        options.Query, options, cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创作者搜索任务失败");
                }
            }, cancellationToken));
        }
        
        // 等待所有搜索任务完成
        await Task.WhenAll(tasks);
        
        return result;
    }
}