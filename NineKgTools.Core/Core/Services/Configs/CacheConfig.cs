using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 缓存配置（启动前通过 config.yaml 固定，不在前端 Settings 页暴露）
/// </summary>
public class CacheConfig
{
    [YamlMember(Alias = "path", Description = "缓存根目录，包括图片与识别缓存等")]
    public string Path { get; set; } = ".cache/";

    [YamlMember(Alias = "expiration_minutes", Description = "识别缓存过期时间（分钟）")]
    public int ExpirationMinutes { get; set; } = 30;

    public CacheConfig Copy()
    {
        return new CacheConfig
        {
            Path = Path,
            ExpirationMinutes = ExpirationMinutes
        };
    }
}
