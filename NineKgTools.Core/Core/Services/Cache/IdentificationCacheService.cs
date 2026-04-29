using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using NineKgTools.Core.Models.Cache;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Configs;
using Serilog;

namespace NineKgTools.Core.Services.Cache;

/// <summary>
/// 识别缓存服务
/// </summary>
public class IdentificationCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly Config _config;
    private readonly TimeSpan _defaultExpiration;
    private readonly object _lockObject = new();
    
    // 统计信息
    private long _totalHits = 0;
    private long _totalMisses = 0;
    private long _totalEvictions = 0;
    
    public IdentificationCacheService(IMemoryCache memoryCache, Config config)
    {
        _memoryCache = memoryCache;
        _config = config;
        
        // 默认缓存30分钟，可以从配置中读取
        _defaultExpiration = TimeSpan.FromMinutes(
            config.Cache?.ExpirationMinutes ?? 30);
    }
    
    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalHits = _totalHits,
            TotalMisses = _totalMisses,
            TotalEvictions = _totalEvictions,
            HitRate = _totalHits + _totalMisses == 0 ? 0 : 
                (double)_totalHits / (_totalHits + _totalMisses)
        };
    }
    
    /// <summary>
    /// 从缓存获取媒体信息
    /// </summary>
    /// <param name="websiteName">网站名称</param>
    /// <param name="websiteId">网站特定ID</param>
    /// <returns>缓存的媒体信息，如果不存在返回null</returns>
    public async Task<MediaBase?> GetAsync(string websiteName, string websiteId)
    {
        var key = GenerateCacheKey(websiteName, websiteId);
        
        if (_memoryCache.TryGetValue<IdentificationCacheEntry>(key, out var entry))
        {
            Interlocked.Increment(ref _totalHits);
            entry.RecordAccess();
            
            Log.Debug("缓存命中: 网站={Website}, ID={Id}, 命中次数={HitCount}", 
                websiteName, websiteId, entry.HitCount);
            
            return entry.Media;
        }
        
        Interlocked.Increment(ref _totalMisses);
        Log.Debug("缓存未命中: 网站={Website}, ID={Id}", websiteName, websiteId);
        
        return null;
    }
    
    /// <summary>
    /// 从缓存获取媒体信息（带选项）
    /// </summary>
    public async Task<MediaBase?> GetAsync(string websiteName, string websiteId, 
        IdentificationOptions? options)
    {
        // 如果配置了跳过缓存，直接返回null
        if (options?.SkipCache == true)
        {
            Log.Debug("根据选项跳过缓存: 网站={Website}, ID={Id}", websiteName, websiteId);
            return null;
        }
        
        return await GetAsync(websiteName, websiteId);
    }
    
    /// <summary>
    /// 设置缓存
    /// </summary>
    /// <param name="websiteName">网站名称</param>
    /// <param name="websiteId">网站特定ID</param>
    /// <param name="media">媒体信息</param>
    /// <param name="source">缓存来源</param>
    /// <param name="expiration">过期时间（可选）</param>
    public async Task SetAsync(string websiteName, string websiteId, 
        MediaBase media, CacheSource source = CacheSource.Automatic, 
        TimeSpan? expiration = null)
    {
        if (media == null)
        {
            Log.Warning("尝试缓存null媒体信息: 网站={Website}, ID={Id}", 
                websiteName, websiteId);
            return;
        }
        
        var key = GenerateCacheKey(websiteName, websiteId);
        var entry = new IdentificationCacheEntry
        {
            Media = media,
            WebsiteName = websiteName,
            WebsiteId = websiteId,
            CacheKey = key,
            Source = source,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            HitCount = 0
        };
        
        var cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = expiration ?? _defaultExpiration,
            Priority = source == CacheSource.Manual ? 
                CacheItemPriority.High : CacheItemPriority.Normal,
            Size = 1 // 设置缓存条目大小
        };
        
        // 注册驱逐回调
        cacheOptions.RegisterPostEvictionCallback((key, value, reason, state) =>
        {
            Interlocked.Increment(ref _totalEvictions);
            Log.Debug("缓存被驱逐: Key={Key}, Reason={Reason}", key, reason);
        });
        
        _memoryCache.Set(key, entry, cacheOptions);
        
        Log.Information("媒体信息已缓存: 网站={Website}, ID={Id}, 标题={Title}, 来源={Source}", 
            websiteName, websiteId, media.Title, source);
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 移除特定缓存
    /// </summary>
    public void Remove(string websiteName, string websiteId)
    {
        var key = GenerateCacheKey(websiteName, websiteId);
        _memoryCache.Remove(key);
        
        Log.Debug("缓存已移除: 网站={Website}, ID={Id}", websiteName, websiteId);
    }
    
    /// <summary>
    /// 清除所有缓存（通过创建新的缓存实例实现）
    /// </summary>
    public void Clear()
    {
        // 注意：IMemoryCache 不直接支持清除所有缓存
        // 这里可以通过维护键列表或使用其他策略
        Log.Warning("清除所有识别缓存（需要实现键追踪机制）");
        
        // 重置统计
        _totalHits = 0;
        _totalMisses = 0;
        _totalEvictions = 0;
    }
    
    /// <summary>
    /// 生成缓存键
    /// </summary>
    private string GenerateCacheKey(string websiteName, string websiteId)
    {
        return $"identification:{websiteName}:{websiteId}".ToLowerInvariant();
    }
    
    /// <summary>
    /// 生成带选项的缓存键
    /// </summary>
    public string GenerateCacheKey(string websiteName, string websiteId, 
        IdentificationOptions? options)
    {
        if (options == null)
        {
            return GenerateCacheKey(websiteName, websiteId);
        }
        
        var keyBuilder = new StringBuilder();
        keyBuilder.Append($"identification:{websiteName}:{websiteId}");
        
        // 添加影响结果的选项到键中
        if (!string.IsNullOrEmpty(options.CustomIdentificationName))
        {
            keyBuilder.Append($":name:{options.CustomIdentificationName}");
        }
        
        // 为了避免键过长，使用哈希
        var keyString = keyBuilder.ToString().ToLowerInvariant();
        if (keyString.Length > 100)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
            return $"identification:{Convert.ToBase64String(hashBytes)}";
        }
        
        return keyString;
    }
    
    /// <summary>
    /// 预热缓存（批量加载）
    /// </summary>
    public async Task WarmupAsync(Dictionary<(string website, string id), MediaBase> items)
    {
        Log.Information("开始预热缓存，共 {Count} 项", items.Count);
        
        foreach (var item in items)
        {
            await SetAsync(item.Key.website, item.Key.id, item.Value, 
                CacheSource.Preloaded);
        }
        
        Log.Information("缓存预热完成");
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalEvictions { get; set; }
    public double HitRate { get; set; }
}