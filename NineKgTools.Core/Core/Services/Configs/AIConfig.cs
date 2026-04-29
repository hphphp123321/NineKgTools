using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class AIConfig
{
    [YamlMember(Alias = "use_ai", Description = "是否使用AI")]
    public bool UseAi { get; set; }

    [YamlMember(Alias = "use_ai_for_keyword_splitting", Description = "是否使用AI进行关键词切分")]
    public bool UseAiForKeywordSplitting { get; set; }

    [YamlMember(Alias = "open_ai", Description = "OpenAI配置")]
    public OpenAi OpenAi { get; set; } = null!;

    [YamlMember(Alias = "vector", Description = "向量配置")]
    public VectorConfig Vector { get; set; } = new();

    public AIConfig Copy()
    {
        return new AIConfig
        {
            UseAi = UseAi,
            UseAiForKeywordSplitting = UseAiForKeywordSplitting,
            OpenAi = OpenAi.Copy(),
            Vector = Vector.Copy()
        };
    }
}

public class OpenAi
{
    [YamlMember(Alias = "api_key", Description = "OpenAI API Key，如果为空会从环境变量获取")]
    public string? ApiKey { get; set; }

    [YamlMember(Alias = "api_version", Description = "OpenAI API版本，即base domain后面的后缀")]
    public string ApiVersion { get; set; } = "v1";

    [YamlMember(Alias = "default_model", Description = "默认模型")]
    public string DefaultModel { get; set; } = "gpt-4o-mini";

    [YamlMember(Alias = "base_domain", Description = "OpenAI API基础域名，注意不能有域名/后面的后缀")]
    public string BaseDomain { get; set; } = "https://api.openai.com";

    public OpenAi Copy()
    {
        return new OpenAi
        {
            ApiKey = ApiKey,
            ApiVersion = ApiVersion,
            DefaultModel = DefaultModel,
            BaseDomain = BaseDomain
        };
    }
}
