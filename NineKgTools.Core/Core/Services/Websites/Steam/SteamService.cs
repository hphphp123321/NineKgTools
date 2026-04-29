using System.Text.Json;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Steam.Models;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Steam;

/// <summary>
/// Steam网站服务，通过公开Storefront API获取游戏数据
/// </summary>
public partial class SteamService(Config config, HttpService http, TagService tagService, MediaNameSplitterService splitterService) : IWebsite
{
    private readonly SteamSearch _steamSearch = new(config, http, splitterService);
    private readonly MediaNameSplitterService _splitterService = splitterService;

    private const string StorefrontBaseUrl = "https://store.steampowered.com/api";

    public string Name => "Steam";

    public List<TopCategory> TopCategories =>
    [
        TopCategory.Game,
        TopCategory.Unknown
    ];

    public bool Enable => config.Website.Steam.Enable;

    private string Language => string.IsNullOrWhiteSpace(config.Website.Steam.Language)
        ? "schinese"
        : config.Website.Steam.Language;

    private string CountryCode => string.IsNullOrWhiteSpace(config.Website.Steam.CountryCode)
        ? "us"
        : config.Website.Steam.CountryCode;

    public MediaBase? GetMediaInfo(MediaSource mediaSource)
    {
        return GetMediaInfoAsync(mediaSource).Result;
    }

    public async Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam] 开始解析: {Path.GetFileName(mediaSource.FullPath)}");
        }

        var appId = await SearchAppIdBySourceAsync(mediaSource, progressReporter, cancellationToken);
        if (appId == 0)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[Steam] 未搜索到匹配的AppID");
            }
            return null;
        }

        return await GetMediaInfoByAppIdAsync(appId, mediaSource, progressReporter, cancellationToken);
    }

    public async Task<MediaBase?> GetMediaInfoAsync(string id, MediaSource? mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam] 通过AppID获取: {id}");
        }

        if (!int.TryParse(id, out var appId) || appId <= 0)
        {
            Log.Warning("Steam AppID 格式无效: {Id}", id);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Steam] AppID格式无效: {id}");
            }
            return null;
        }

        mediaSource ??= MediaSourceFactory.Create();
        return await GetMediaInfoByAppIdAsync(appId, mediaSource, progressReporter, cancellationToken);
    }

    public async Task<PriorityQueue<MediaSearchResult, double>> SearchMediaAsync(MediaSource mediaSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _steamSearch.SearchAppsAsync(mediaSource, cancellationToken);
        if (results == null || results.Count == 0) return new PriorityQueue<MediaSearchResult, double>();

        var name = Path.GetFileNameWithoutExtension(mediaSource.FullPath);
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);
        return _steamSearch.GeneratePriorityQueueWithRelevanceScoring(results, keywords);
    }

    #region 私有方法

    /// <summary>
    /// 根据媒体源搜索并返回最佳匹配的 AppID
    /// </summary>
    private async Task<int> SearchAppIdBySourceAsync(MediaSource mediaSource, IProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var results = await _steamSearch.SearchAppsAsync(mediaSource, cancellationToken);
        if (results == null || results.Count == 0)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[Steam] 搜索无结果");
            }
            return 0;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam] 搜索到 {results.Count} 条结果");
        }

        var appId = await _steamSearch.MatchAppIdByName(results, mediaSource.GetFileName(), cancellationToken);
        if (appId != 0 && progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam] 匹配到AppID: {appId}");
        }

        return appId;
    }

    /// <summary>
    /// 通过 AppID 调用 Steam Storefront appdetails 接口并映射为 GameMedia
    /// </summary>
    private async Task<MediaBase?> GetMediaInfoByAppIdAsync(int appId, MediaSource mediaSource, IProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var data = await FetchAppDetailsAsync(appId, cancellationToken);
        if (data == null)
        {
            Log.Warning("Steam appdetails 返回为空, appId={AppId}", appId);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Steam] 未获取到AppID {appId} 的详情");
            }
            return null;
        }

        if (!string.Equals(data.Type, "game", StringComparison.OrdinalIgnoreCase))
        {
            Log.Debug("Steam AppID {AppId} 的类型为 {Type}，不是游戏，跳过", appId, data.Type);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Steam] AppID {appId} 类型为 {data.Type}，非游戏，跳过");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam] 获取到条目: {data.Name}");
        }

        return await ConvertAppDataToGameMediaAsync(data, mediaSource, progressReporter, cancellationToken);
    }

    /// <summary>
    /// 调用 Steam appdetails 接口获取单个 app 的详情
    /// </summary>
    private async Task<SteamAppData?> FetchAppDetailsAsync(int appId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = $"{StorefrontBaseUrl}/appdetails?appids={appId}&cc={CountryCode}&l={Language}";
        var response = await http.Get(url, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            Log.Warning("Steam appdetails 无响应: {Url}", url);
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Dictionary<string, SteamAppDetailsEntry>>(response);
            if (envelope == null || !envelope.TryGetValue(appId.ToString(), out var entry))
            {
                Log.Warning("Steam appdetails 响应中未找到 AppID {AppId}", appId);
                return null;
            }

            if (!entry.Success || entry.Data == null)
            {
                Log.Warning("Steam appdetails 返回 success=false, AppID={AppId} (可能当前区域不可见，尝试更换 country_code)", appId);
                return null;
            }

            return entry.Data;
        }
        catch (Exception e)
        {
            Log.Error(e, "解析Steam appdetails 响应失败, url: {Url}", url);
            return null;
        }
    }

    #endregion
}
