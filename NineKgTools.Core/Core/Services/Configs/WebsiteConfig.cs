using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class WebsiteConfig
{
    [YamlMember(Alias = "priority", Description = "网站识别的优先级")]
    public WebsitePriority Priority { get; set; } = null!;

    [YamlMember(Alias = "dlsite", Description = "Dlsite配置")]
    public DLsiteConfig DLsite { get; set; } = null!;

    [YamlMember(Alias = "bangumi", Description = "Bangumi配置")]
    public BangumiConfig Bangumi { get; set; } = null!;

    [YamlMember(Alias = "steam", Description = "Steam配置")]
    public SteamConfig Steam { get; set; } = new();

    public WebsiteConfig Copy()
    {
        return new WebsiteConfig
        {
            DLsite = DLsite.Copy(),
            Bangumi = Bangumi.Copy(),
            Steam = Steam?.Copy() ?? new SteamConfig(),
            Priority = Priority.Copy()
        };
    }
}

public class WebsitePriority
{
    [YamlMember(Alias = "audio", Description = "音频顺序")]
    public List<string> Audio { get; set; } = new();
    
    [YamlMember(Alias = "video", Description = "视频顺序")]
    public List<string> Video { get; set; } = new();
    
    [YamlMember(Alias = "game", Description = "游戏顺序")]
    public List<string> Game { get; set; } = new();
    
    [YamlMember(Alias = "text", Description = "文本顺序")]
    public List<string> Text { get; set; } = new();
    
    [YamlMember(Alias = "picture", Description = "图片顺序")]
    public List<string> Picture { get; set; } = new();
    
    [YamlMember(Alias = "unknown", Description = "未知媒体即默认的识别顺序")]
    public List<string> Unknown { get; set; } = new();
    
    public WebsitePriority Copy()
    {
        return new WebsitePriority
        {
            Audio = new List<string>(Audio),
            Video = new List<string>(Video),
            Game = new List<string>(Game),
            Text = new List<string>(Text),
            Picture = new List<string>(Picture),
            Unknown = new List<string>(Unknown)
        };
    }
}

public class DLsiteConfig
{
    [YamlMember(Alias = "enable", Description = "是否启用DLsite")]
    public bool Enable { get; set; }
    
    [YamlMember(Alias = "use_selenium_for_rating", Description = "是否使用Selenium获取评分")]
    public bool UseSeleniumForRating { get; set; }
    
    public DLsiteConfig Copy()
    {
        return new DLsiteConfig
        {
            Enable = Enable,
            UseSeleniumForRating = UseSeleniumForRating
        };
    }
}

public class BangumiConfig
{
    [YamlMember(Alias = "enable", Description = "是否启用Bangumi")]
    public bool Enable { get; set; }

    [YamlMember(Alias = "api_key", Description = "Bangumi API Key, 从https://next.bgm.tv/demo/access-token申请")]
    public string ApiKey { get; set; } = string.Empty;

    public BangumiConfig Copy()
    {
        return new BangumiConfig
        {
            Enable = Enable,
            ApiKey = ApiKey
        };
    }
}

public class SteamConfig
{
    [YamlMember(Alias = "enable", Description = "是否启用Steam")]
    public bool Enable { get; set; } = true;

    [YamlMember(Alias = "language", Description = "请求语言，影响name/description本地化。常见值：schinese / english / japanese")]
    public string Language { get; set; } = "schinese";

    [YamlMember(Alias = "country_code", Description = "国家代码，影响价格/发行区域。禁用cn（部分游戏对CN区未开放），推荐us")]
    public string CountryCode { get; set; } = "us";

    public SteamConfig Copy()
    {
        return new SteamConfig
        {
            Enable = Enable,
            Language = Language,
            CountryCode = CountryCode
        };
    }
}