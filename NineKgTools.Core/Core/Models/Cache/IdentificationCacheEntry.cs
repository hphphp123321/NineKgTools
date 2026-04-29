using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Models.Cache;

/// <summary>
/// 识别缓存条目
/// </summary>
public class IdentificationCacheEntry
{
    /// <summary>
    /// 缓存的媒体信息
    /// </summary>
    public MediaBase Media { get; set; } = null!;
    
    /// <summary>
    /// 缓存创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime LastAccessedAt { get; set; }
    
    /// <summary>
    /// 缓存命中次数
    /// </summary>
    public int HitCount { get; set; }
    
    /// <summary>
    /// 缓存来源
    /// </summary>
    public CacheSource Source { get; set; }
    
    /// <summary>
    /// 网站名称
    /// </summary>
    public string WebsiteName { get; set; } = null!;
    
    /// <summary>
    /// 网站特定ID
    /// </summary>
    public string WebsiteId { get; set; } = null!;
    
    /// <summary>
    /// 缓存键
    /// </summary>
    public string CacheKey { get; set; } = null!;
    
    /// <summary>
    /// 是否已过期
    /// </summary>
    public bool IsExpired(TimeSpan expiration)
    {
        return DateTime.UtcNow - CreatedAt > expiration;
    }
    
    /// <summary>
    /// 记录访问
    /// </summary>
    public void RecordAccess()
    {
        HitCount++;
        LastAccessedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// 缓存来源类型
/// </summary>
public enum CacheSource
{
    /// <summary>
    /// 自动识别产生的缓存
    /// </summary>
    Automatic,
    
    /// <summary>
    /// 手动识别产生的缓存
    /// </summary>
    Manual,
    
    /// <summary>
    /// 预加载的缓存
    /// </summary>
    Preloaded,
    
    /// <summary>
    /// 批量导入的缓存
    /// </summary>
    BatchImport
}