using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Steam.Models;

/// <summary>
/// Steam Storefront appdetails 顶层响应，key 为 appid 字符串
/// </summary>
public class SteamAppDetailsEntry
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public SteamAppData? Data { get; set; }
}

/// <summary>
/// Steam 应用的详细数据
/// </summary>
public class SteamAppData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("steam_appid")]
    public int SteamAppid { get; set; }

    [JsonPropertyName("required_age")]
    public int RequiredAge { get; set; }

    [JsonPropertyName("is_free")]
    public bool IsFree { get; set; }

    [JsonPropertyName("detailed_description")]
    public string? DetailedDescription { get; set; }

    [JsonPropertyName("about_the_game")]
    public string? AboutTheGame { get; set; }

    [JsonPropertyName("short_description")]
    public string? ShortDescription { get; set; }

    [JsonPropertyName("supported_languages")]
    public string? SupportedLanguages { get; set; }

    [JsonPropertyName("header_image")]
    public string? HeaderImage { get; set; }

    [JsonPropertyName("capsule_image")]
    public string? CapsuleImage { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("developers")]
    public List<string>? Developers { get; set; }

    [JsonPropertyName("publishers")]
    public List<string>? Publishers { get; set; }

    [JsonPropertyName("platforms")]
    public SteamPlatforms? Platforms { get; set; }

    [JsonPropertyName("metacritic")]
    public SteamMetacritic? Metacritic { get; set; }

    [JsonPropertyName("categories")]
    public List<SteamCategory>? Categories { get; set; }

    [JsonPropertyName("genres")]
    public List<SteamGenre>? Genres { get; set; }

    [JsonPropertyName("screenshots")]
    public List<SteamScreenshot>? Screenshots { get; set; }

    [JsonPropertyName("release_date")]
    public SteamReleaseDate? ReleaseDate { get; set; }
}

public class SteamPlatforms
{
    [JsonPropertyName("windows")]
    public bool Windows { get; set; }

    [JsonPropertyName("mac")]
    public bool Mac { get; set; }

    [JsonPropertyName("linux")]
    public bool Linux { get; set; }
}

public class SteamMetacritic
{
    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class SteamCategory
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class SteamGenre
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class SteamScreenshot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("path_thumbnail")]
    public string? PathThumbnail { get; set; }

    [JsonPropertyName("path_full")]
    public string? PathFull { get; set; }
}

public class SteamReleaseDate
{
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }
}
