using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 标签匹配配置
/// </summary>
public class TagMatchingConfig
{
    [YamlMember(Alias = "enable_fuzzy_matching", Description = "是否启用模糊匹配（默认启用）")]
    public bool EnableFuzzyMatching { get; set; } = true;

    [YamlMember(Alias = "similarity_threshold", Description = "相似度匹配阈值（0-1之间，默认0.7）")]
    public double SimilarityThreshold { get; set; } = 0.7;

    [YamlMember(Alias = "enable_contains_matching", Description = "是否启用包含匹配（默认启用）")]
    public bool EnableContainsMatching { get; set; } = true;

    [YamlMember(Alias = "enable_normalized_matching", Description = "是否启用规范化匹配（默认启用）")]
    public bool EnableNormalizedMatching { get; set; } = true;

    [YamlMember(Alias = "max_match_results", Description = "返回的最大匹配结果数（默认5）")]
    public int MaxMatchResults { get; set; } = 5;

    [YamlMember(Alias = "log_match_details", Description = "是否记录匹配详情（用于调试，默认关闭）")]
    public bool LogMatchDetails { get; set; } = false;

    public TagMatchingConfig Copy()
    {
        return new TagMatchingConfig
        {
            EnableFuzzyMatching = EnableFuzzyMatching,
            SimilarityThreshold = SimilarityThreshold,
            EnableContainsMatching = EnableContainsMatching,
            EnableNormalizedMatching = EnableNormalizedMatching,
            MaxMatchResults = MaxMatchResults,
            LogMatchDetails = LogMatchDetails
        };
    }
}
