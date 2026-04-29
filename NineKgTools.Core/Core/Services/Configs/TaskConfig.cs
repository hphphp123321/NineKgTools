using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 缓存清理配置
/// </summary>
public class CacheCleanupSettings
{
    [YamlMember(Alias = "max_age_days", Description = "缓存文件最大保留天数，0表示不按时间清理")]
    public int MaxAgeDays { get; set; } = 0;

    [YamlMember(Alias = "cleanup_image_cache", Description = "是否清理过期图片缓存")]
    public bool CleanupImageCache { get; set; } = true;

    [YamlMember(Alias = "cleanup_temp_files", Description = "是否清理临时文件")]
    public bool CleanupTempFiles { get; set; } = true;

    public CacheCleanupSettings Copy() => new()
    {
        MaxAgeDays = MaxAgeDays,
        CleanupImageCache = CleanupImageCache,
        CleanupTempFiles = CleanupTempFiles
    };
}

/// <summary>
/// 定时任务配置
/// </summary>
public class TaskConfig
{
    [YamlMember(Alias = "scheduled_tasks", Description = "定时任务配置列表")]
    public List<ScheduledTaskConfig> ScheduledTasks { get; set; } = new();

    [YamlMember(Alias = "retry_count", Description = "任务失败重试次数")]
    public int RetryCount { get; set; } = 3;

    [YamlMember(Alias = "max_concurrent_identification_tasks", Description = "最大并发识别任务数")]
    public int MaxConcurrentIdentificationTasks { get; set; } = 5;

    [YamlMember(Alias = "cache_cleanup", Description = "缓存清理配置")]
    public CacheCleanupSettings CacheCleanup { get; set; } = new();

    public TaskConfig Copy()
    {
        return new TaskConfig
        {
            ScheduledTasks = ScheduledTasks?.Select(t => new ScheduledTaskConfig
            {
                Name = t.Name,
                Type = t.Type,
                CronExpression = t.CronExpression,
                Enabled = t.Enabled,
                Description = t.Description,
                Parameters = t.Parameters?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                TimeoutOverride = t.TimeoutOverride,
                Priority = t.Priority
            }).ToList() ?? new List<ScheduledTaskConfig>(),
            RetryCount = RetryCount,
            MaxConcurrentIdentificationTasks = MaxConcurrentIdentificationTasks,
            CacheCleanup = CacheCleanup?.Copy() ?? new CacheCleanupSettings()
        };
    }
}

/// <summary>
/// 单个定时任务配置
/// </summary>
public class ScheduledTaskConfig
{
    [YamlMember(Alias = "name", Description = "任务名称")]
    public string Name { get; set; } = null!;
    
    [YamlMember(Alias = "type", Description = "任务类型")]
    public string Type { get; set; } = null!;
    
    [YamlMember(Alias = "cron", Description = "Cron表达式")]
    public string CronExpression { get; set; } = null!;
    
    [YamlMember(Alias = "enabled", Description = "是否启用")]
    public bool Enabled { get; set; } = true;
    
    [YamlMember(Alias = "description", Description = "任务描述")]
    public string? Description { get; set; }
    
    [YamlMember(Alias = "parameters", Description = "任务参数")]
    public Dictionary<string, object>? Parameters { get; set; }
    
    [YamlMember(Alias = "timeout_override", Description = "覆盖默认超时时间(分钟)")]
    public int? TimeoutOverride { get; set; }
    
    [YamlMember(Alias = "priority", Description = "任务优先级")]
    public string? Priority { get; set; }
    
    [YamlIgnore]
    public string TaskType => Type;
}