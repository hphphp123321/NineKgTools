using NineKgTools.Core.Models.Categories;

namespace NineKgTools.Core.Models.Identification;

/// <summary>
/// 识别选项构建器（流式API）
/// </summary>
public class IdentificationOptionsBuilder
{
    private readonly IdentificationOptions _options;
    
    public IdentificationOptionsBuilder()
    {
        _options = new IdentificationOptions();
    }
    
    /// <summary>
    /// 设置优先网站
    /// </summary>
    public IdentificationOptionsBuilder WithWebsite(string website)
    {
        _options.PreferredWebsite = website;
        return this;
    }
    
    /// <summary>
    /// 设置网站特定ID
    /// </summary>
    public IdentificationOptionsBuilder WithId(string id)
    {
        _options.WebsiteSpecificId = id;
        return this;
    }
    
    /// <summary>
    /// 设置自定义名称
    /// </summary>
    public IdentificationOptionsBuilder WithName(string name)
    {
        _options.CustomIdentificationName = name;
        return this;
    }
    
    /// <summary>
    /// 设置识别策略
    /// </summary>
    public IdentificationOptionsBuilder WithStrategy(IdentificationStrategy strategy)
    {
        _options.Strategy = strategy;
        return this;
    }
    
    /// <summary>
    /// 设置超时时间
    /// </summary>
    public IdentificationOptionsBuilder WithTimeout(TimeSpan timeout)
    {
        _options.Timeout = timeout;
        return this;
    }
    
    /// <summary>
    /// 跳过缓存
    /// </summary>
    public IdentificationOptionsBuilder SkipCache()
    {
        _options.SkipCache = true;
        return this;
    }
    
    /// <summary>
    /// 添加网站ID映射
    /// </summary>
    public IdentificationOptionsBuilder AddWebsiteId(string website, string id)
    {
        if (_options.WebsiteIds == null)
            _options.WebsiteIds = new Dictionary<string, string>();
        
        _options.WebsiteIds[website] = id;
        return this;
    }
    
    /// <summary>
    /// 添加元数据
    /// </summary>
    public IdentificationOptionsBuilder AddMetadata(string key, object value)
    {
        if (_options.Metadata == null)
            _options.Metadata = new Dictionary<string, object>();
        
        _options.Metadata[key] = value;
        return this;
    }
    
    /// <summary>
    /// 设置源路径
    /// </summary>
    public IdentificationOptionsBuilder WithSourcePath(string path)
    {
        _options.SourcePath = path;
        return this;
    }
    
    /// <summary>
    /// 设置推测的媒体类型
    /// </summary>
    public IdentificationOptionsBuilder WithSuggestedCategory(TopCategory category)
    {
        _options.SuggestedCategory = category;
        return this;
    }
    
    /// <summary>
    /// 设置网站优先级覆盖
    /// </summary>
    public IdentificationOptionsBuilder WithWebsitePriority(params string[] websites)
    {
        _options.WebsitePriorityOverride = websites.ToList();
        return this;
    }
    
    /// <summary>
    /// 使用快速识别配置
    /// </summary>
    public IdentificationOptionsBuilder UseQuickMode()
    {
        _options.Strategy = IdentificationStrategy.Quick;
        _options.Timeout = TimeSpan.FromSeconds(10);
        return this;
    }
    
    /// <summary>
    /// 使用手动识别配置
    /// </summary>
    public IdentificationOptionsBuilder UseManualMode()
    {
        _options.Strategy = IdentificationStrategy.Manual;
        _options.SkipCache = true;
        return this;
    }
    
    /// <summary>
    /// 使用混合模式配置
    /// </summary>
    public IdentificationOptionsBuilder UseHybridMode()
    {
        _options.Strategy = IdentificationStrategy.Hybrid;
        return this;
    }
    
    /// <summary>
    /// 使用强制刷新配置
    /// </summary>
    public IdentificationOptionsBuilder UseForceRefresh()
    {
        _options.Strategy = IdentificationStrategy.ForceRefresh;
        _options.SkipCache = true;
        return this;
    }
    
    /// <summary>
    /// 使用仅缓存配置
    /// </summary>
    public IdentificationOptionsBuilder UseCacheOnly()
    {
        _options.Strategy = IdentificationStrategy.CacheOnly;
        return this;
    }
    
    /// <summary>
    /// 构建选项对象
    /// </summary>
    public IdentificationOptions Build()
    {
        var validationResult = _options.Validate();
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"无效的识别选项配置: {validationResult.GetErrorMessage()}");
        }
        
        return _options;
    }
    
    /// <summary>
    /// 构建选项对象（不验证）
    /// </summary>
    public IdentificationOptions BuildWithoutValidation()
    {
        return _options;
    }
}