using NineKgTools.Core.Models.Identification;
using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 媒体识别默认配置
/// </summary>
public class IdentificationConfig
{
    #region 高级选项

    /// <summary>
    /// 是否跳过缓存，强制重新识别
    /// </summary>
    [YamlMember(Alias = "skip_cache", Description = "是否跳过缓存，强制重新识别")]
    public bool SkipCache { get; set; } = false;

    /// <summary>
    /// 单个网站的识别超时时间（秒）
    /// </summary>
    [YamlMember(Alias = "timeout_seconds", Description = "单个网站的识别超时时间（秒）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 最小相似度阈值，低于此相似度的搜索结果将被忽略
    /// </summary>
    [YamlMember(Alias = "min_similarity", Description = "最小相似度，低于此相似度的搜索结果将被忽略")]
    public double MinSimilarity { get; set; }

    #endregion

    #region 策略控制

    /// <summary>
    /// 识别策略
    /// </summary>
    [YamlMember(Alias = "strategy", Description = "识别策略 (Auto, Manual, Hybrid, ForceRefresh, CacheOnly, Quick)")]
    public IdentificationStrategy Strategy { get; set; } = IdentificationStrategy.Auto;

    /// <summary>
    /// 覆盖默认的网站优先级列表
    /// </summary>
    [YamlMember(Alias = "website_priority_override", Description = "覆盖默认的网站优先级列表")]
    public List<string>? WebsitePriorityOverride { get; set; }

    /// <summary>
    /// 识别完成后是否自动添加到数据库（仅影响后台识别任务）。
    /// 关闭时后台识别结果会落到 PendingIdentification 表中，等待用户在"待处理"页面人工确认入库。
    /// 手动识别入口不受此开关影响，始终要求在 MediaInfoDialog 上人工确认。
    /// </summary>
    [YamlMember(Alias = "auto_add_to_database", Description = "识别完成后是否自动添加到数据库（仅影响后台识别任务）")]
    public bool AutoAddToDatabase { get; set; } = true;

    /// <summary>
    /// 待入库识别结果的保留天数。
    /// 超过此天数且仍未入库的 PendingIdentification 记录会被后台清理服务清理掉，
    /// 对应 MediaSource 的 Identified 标记会被置回 false。
    /// 0 表示永不清理。
    /// </summary>
    [YamlMember(Alias = "pending_retention_days", Description = "待入库识别结果的保留天数（0 = 永不清理）")]
    public int PendingRetentionDays { get; set; } = 30;

    #endregion

    /// <summary>
    /// 复制配置
    /// </summary>
    public IdentificationConfig Copy()
    {
        return new IdentificationConfig
        {
            SkipCache = SkipCache,
            TimeoutSeconds = TimeoutSeconds,
            MinSimilarity = MinSimilarity,
            Strategy = Strategy,
            WebsitePriorityOverride = WebsitePriorityOverride?.ToList(),
            AutoAddToDatabase = AutoAddToDatabase,
            PendingRetentionDays = PendingRetentionDays
        };
    }

    /// <summary>
    /// 转换为 IdentificationOptions 对象
    /// </summary>
    public IdentificationOptions ToIdentificationOptions()
    {
        return new IdentificationOptions
        {
            SkipCache = SkipCache,
            Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
            Strategy = Strategy,
            WebsitePriorityOverride = WebsitePriorityOverride?.ToList(),
            AutoAddToDatabase = AutoAddToDatabase
        };
    }
}
