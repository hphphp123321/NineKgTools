using NineKgTools.Core.Models.Categories;

namespace NineKgTools.Core.Models.Identification;

/// <summary>
/// 媒体识别选项配置类
/// </summary>
public class IdentificationOptions
{
    
    public IdentificationOptions()
    {
    }
    
    #region 基础选项

    /// <summary>
    /// 指定网站名称（仅使用此网站进行识别，不查询其他网站）
    /// </summary>
    public string? PreferredWebsite { get; set; }
    
    /// <summary>
    /// 网站特定的媒体ID（如DLsite的RJ号，Bangumi的数字ID）
    /// </summary>
    public string? WebsiteSpecificId { get; set; }
    
    /// <summary>
    /// 自定义识别名称，用于搜索或覆盖默认名称
    /// </summary>
    public string? CustomIdentificationName { get; set; }
    
    #endregion
    
    #region 高级选项
    
    /// <summary>
    /// 多网站ID映射，支持同时指定多个网站的ID
    /// Key: 网站名称, Value: 对应的ID
    /// </summary>
    public Dictionary<string, string>? WebsiteIds { get; set; }
    
    /// <summary>
    /// 是否跳过缓存，强制重新识别
    /// </summary>
    public bool SkipCache { get; set; } = false;
    
    /// <summary>
    /// 单个网站的识别超时时间
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    
    #endregion
    
    #region 策略控制
    
    /// <summary>
    /// 识别策略
    /// </summary>
    public IdentificationStrategy Strategy { get; set; } = IdentificationStrategy.Auto;
    
    /// <summary>
    /// 覆盖默认的网站优先级列表
    /// </summary>
    public List<string>? WebsitePriorityOverride { get; set; }

    /// <summary>
    /// 识别完成后是否自动添加到数据库（默认: true）
    /// </summary>
    public bool AutoAddToDatabase { get; set; } = true;

    #endregion

    #region 元数据和上下文
    
    /// <summary>
    /// 源路径（可选，用于关联文件系统）
    /// </summary>
    public string? SourcePath { get; set; }
    
    /// <summary>
    /// 推测的媒体类型（可选，用于优化识别）
    /// </summary>
    public TopCategory? SuggestedCategory { get; set; }
    
    /// <summary>
    /// 额外的元数据，用于传递自定义信息
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
    
    #endregion
    
    #region 验证和辅助方法
    
    /// <summary>
    /// 验证选项配置是否有效
    /// </summary>
    public ValidationResult Validate()
    {
        var errors = new List<string>();
        
        // 验证超时时间
        if (Timeout <= TimeSpan.Zero)
        {
            errors.Add("超时时间必须大于0");
        }
        
        // 验证策略特定的配置
        if (Strategy == IdentificationStrategy.Manual)
        {
            if (string.IsNullOrEmpty(PreferredWebsite) && 
                (WebsiteIds == null || WebsiteIds.Count == 0))
            {
                errors.Add("手动模式需要指定网站和ID");
            }
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    /// <summary>
    /// 检查是否配置了手动识别信息
    /// </summary>
    public bool HasManualIdentification()
    {
        return !string.IsNullOrEmpty(WebsiteSpecificId) ||
               (WebsiteIds != null && WebsiteIds.Count > 0);
    }
    
    /// <summary>
    /// 获取指定网站的ID
    /// </summary>
    public string? GetWebsiteId(string websiteName)
    {
        // 如果指定了特定网站且网站名称匹配，返回其ID
        if (string.Equals(PreferredWebsite, websiteName, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(WebsiteSpecificId))
                return WebsiteSpecificId;
        }
        
        // 从字典中查找
        if (WebsiteIds != null && 
            WebsiteIds.TryGetValue(websiteName, out var id))
        {
            return id;
        }
        
        return null;
    }
    
    /// <summary>
    /// 创建默认配置
    /// </summary>
    public static IdentificationOptions CreateDefault()
    {
        return new IdentificationOptions();
    }
    
    /// <summary>
    /// 创建手动识别配置
    /// </summary>
    public static IdentificationOptions CreateManual(string website, string id, string? name = null)
    {
        return new IdentificationOptions
        {
            Strategy = IdentificationStrategy.Manual,
            PreferredWebsite = website,
            WebsiteSpecificId = id,
            CustomIdentificationName = name,
            SkipCache = true
        };
    }
    
    /// <summary>
    /// 创建快速识别配置（跳过某些耗时操作）
    /// </summary>
    public static IdentificationOptions CreateQuick()
    {
        return new IdentificationOptions
        {
            Strategy = IdentificationStrategy.Auto,
            Timeout = TimeSpan.FromSeconds(10)
        };
    }
    
    /// <summary>
    /// 克隆当前配置
    /// </summary>
    public IdentificationOptions Clone()
    {
        return new IdentificationOptions
        {
            PreferredWebsite = PreferredWebsite,
            WebsiteSpecificId = WebsiteSpecificId,
            CustomIdentificationName = CustomIdentificationName,
            WebsiteIds = WebsiteIds != null ? new Dictionary<string, string>(WebsiteIds) : null,
            SkipCache = SkipCache,
            Timeout = Timeout,
            Strategy = Strategy,
            WebsitePriorityOverride = WebsitePriorityOverride?.ToList(),
            AutoAddToDatabase = AutoAddToDatabase,
            SourcePath = SourcePath,
            SuggestedCategory = SuggestedCategory,
            Metadata = Metadata != null ? new Dictionary<string, object>(Metadata) : null
        };
    }
    
    /// <summary>
    /// 重置为默认值
    /// </summary>
    public void Reset()
    {
        PreferredWebsite = null;
        WebsiteSpecificId = null;
        CustomIdentificationName = null;
        WebsiteIds = null;
        SkipCache = false;
        Timeout = TimeSpan.FromSeconds(30);
        Strategy = IdentificationStrategy.Auto;
        WebsitePriorityOverride = null;
        AutoAddToDatabase = true;
        SourcePath = null;
        SuggestedCategory = null;
        Metadata = null;
    }
    
    #endregion
}

/// <summary>
/// 识别策略枚举
/// </summary>
public enum IdentificationStrategy
{
    /// <summary>
    /// 自动模式：按照默认流程识别
    /// </summary>
    Auto,
    
    /// <summary>
    /// 手动模式：使用指定的网站和ID
    /// </summary>
    Manual,
    
    /// <summary>
    /// 混合模式：先尝试手动指定，失败后自动识别
    /// </summary>
    Hybrid,
    
    /// <summary>
    /// 强制刷新：忽略缓存，重新识别
    /// </summary>
    ForceRefresh,
    
    /// <summary>
    /// 仅缓存：只从缓存获取，不进行网络请求
    /// </summary>
    CacheOnly,
    
    /// <summary>
    /// 快速模式：使用更激进的超时和并行策略
    /// </summary>
    Quick
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
    
    public string GetErrorMessage()
    {
        return string.Join("; ", Errors);
    }
}