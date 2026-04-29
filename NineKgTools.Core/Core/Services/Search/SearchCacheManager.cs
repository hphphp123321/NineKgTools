using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Configs;

namespace NineKgTools.Core.Services.Search;

/// <summary>
/// 搜索缓存管理器
/// </summary>
public class SearchCacheManager
{
    private readonly IMemoryCache _cache;
    private readonly SearchConfig _searchConfig;
    private readonly TimeSpan _defaultExpiration;
    
    public SearchCacheManager(IMemoryCache cache, SearchConfig? searchConfig = null)
    {
        _cache = cache;
        _searchConfig = searchConfig ?? new SearchConfig();
        _defaultExpiration = TimeSpan.FromMinutes(_searchConfig.CacheExpirationMinutes);
    }
    
    /// <summary>
    /// 获取缓存的搜索结果
    /// </summary>
    public async Task<GlobalSearchResult?> GetCachedResultAsync(GlobalSearchOptions options)
    {
        // 如果缓存被禁用，直接返回null
        if (!_searchConfig.EnableSearchCache)
        {
            return null;
        }
        
        var key = GenerateCacheKey(options);
        
        if (_cache.TryGetValue<GlobalSearchResult>(key, out var cachedResult))
        {
            return cachedResult;
        }
        
        return null;
    }
    
    /// <summary>
    /// 设置缓存的搜索结果
    /// </summary>
    public async Task SetCachedResultAsync(
        GlobalSearchOptions options, 
        GlobalSearchResult result,
        TimeSpan? expiration = null)
    {
        // 如果缓存被禁用，不进行缓存操作
        if (!_searchConfig.EnableSearchCache)
        {
            return;
        }
        
        // 不缓存被取消的搜索或错误结果
        if (result.WasCancelled || !string.IsNullOrEmpty(result.ErrorMessage))
            return;
        
        // 不缓存空查询的结果
        if (string.IsNullOrWhiteSpace(options.Query))
            return;
        
        var key = GenerateCacheKey(options);
        var cacheExpiration = expiration ?? _defaultExpiration;
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = cacheExpiration,
            Priority = CacheItemPriority.Normal,
            Size = 1 // 设置缓存条目大小
        };
        
        _cache.Set(key, result, cacheOptions);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 清除所有搜索缓存
    /// </summary>
    public void ClearAll()
    {
        // IMemoryCache 不支持清除所有缓存
        // 这里我们可以维护一个缓存键列表，或者使用其他策略
        // 为简化起见，这里不实现具体逻辑
    }
    
    /// <summary>
    /// 生成缓存键
    /// </summary>
    private string GenerateCacheKey(GlobalSearchOptions options)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append("search:");
        keyBuilder.Append(options.Query?.ToLowerInvariant() ?? "");
        keyBuilder.Append(":");
        keyBuilder.Append(options.EnableVectorSearch);
        keyBuilder.Append(":");
        keyBuilder.Append((int)options.EntityTypes);
        
        // 添加过滤器到键
        if (options.CategoryFilter != null)
        {
            keyBuilder.Append(":cat:");
            keyBuilder.Append(string.Join(",", options.CategoryFilter.CategoryIds));
            keyBuilder.Append(":");
            keyBuilder.Append(options.CategoryFilter.Mode);
        }
        
        if (options.TagFilter != null)
        {
            keyBuilder.Append(":tag:");
            keyBuilder.Append(string.Join(",", options.TagFilter.TagIds));
            keyBuilder.Append(":");
            keyBuilder.Append(options.TagFilter.Mode);
        }
        
        if (options.RatingFilter != null)
        {
            keyBuilder.Append(":rating:");
            keyBuilder.Append(options.RatingFilter.MinRating);
            keyBuilder.Append("-");
            keyBuilder.Append(options.RatingFilter.MaxRating);
        }
        
        keyBuilder.Append(":");
        keyBuilder.Append(options.MaxResultsPerType);
        keyBuilder.Append(":");
        keyBuilder.Append(options.MinRelevanceScore);
        
        // 为了避免键过长，使用哈希
        var keyString = keyBuilder.ToString();
        if (keyString.Length > 100)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
            return "search:" + Convert.ToBase64String(hashBytes);
        }
        
        return keyString;
    }
}