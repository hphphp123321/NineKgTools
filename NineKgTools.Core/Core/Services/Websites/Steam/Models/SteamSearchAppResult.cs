using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Steam.Models;

/// <summary>
/// Steam Community SearchApps 接口返回项
/// https://steamcommunity.com/actions/SearchApps/{query}
/// </summary>
public class SteamSearchAppResult
{
    [JsonPropertyName("appid")]
    public string Appid { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}
