using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.ScraperInterfaces;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

public partial class DLsiteService
    : IWebsite
{
    private Config _config;
    private HttpService _http;
    private TagService _tagService;
    private DLsiteSearch _dlsiteSearch;
    
    public DLsiteService(
        Config config,
        HttpService http,
        TagService tagService,
        MediaNameSplitterService splitterService)
    {
        _config = config;
        _http = http;
        _tagService = tagService;
        _dlsiteSearch = new DLsiteSearch(config, http, this, splitterService);
    }
    
    private const string JpLang = "ja-JP";
    public const string CnLang = "zh-CN";
    private const string EnLang = "en-US";

    public string Name => "DLsite";

    public List<TopCategory> TopCategories =>
    [
        TopCategory.Audio, // 音声
        TopCategory.Video, // 视频
        TopCategory.Game, // 游戏
        TopCategory.Picture // 图片
    ];

    public bool Enable => _config.Website.DLsite.Enable;

    public async Task<PriorityQueue<MediaSearchResult, double>> SearchMediaAsync(MediaSource mediaSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 获取文件夹或者文件名
        var mediaName = Path.GetFileNameWithoutExtension(mediaSource.FullPath);

        var searchPq = await _dlsiteSearch.TrySearchUrlsByName(mediaName, cancellationToken);

        return searchPq;
    }

    public MediaBase? GetMediaInfo(MediaSource mediaSource)
    {
        return GetMediaInfoAsync(mediaSource).Result;
    }

    public async Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 开始解析: {Path.GetFileName(mediaSource.FullPath)}");
        }

        var htmlDocument = await GetHtmlDocumentAsync(mediaSource, progressReporter, cancellationToken);

        if (htmlDocument == null)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[DLsite] 未找到匹配页面");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[DLsite] 开始解析页面内容");
        }

        return await this.ScrapeMediaFromHtmlAsync(mediaSource, htmlDocument, progressReporter, cancellationToken);
    }

    public async Task<MediaBase?> GetMediaInfoAsync(string id, MediaSource? mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 尝试通过ID获取: {id}");
        }

        var dlsiteCode = DLsiteUtils.TryGetDLsiteCodeByName(id);
        if (dlsiteCode == null)
        {
            Log.Debug("id不符合DLsite格式：{Id}，格式应为RJ/VJ/BJ开头", id);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[DLsite] ID格式无效: {id}，应为RJ/VJ/BJ开头");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 解析到代码: {dlsiteCode}");
        }

        var url = DLsiteUtils.GetUrlByDLsiteCode(dlsiteCode);

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 开始抓取页面: {url}");
        }

        var htmlDocument =
            await _http.Scrape(url, HttpMethod.Get, cancellationToken: cancellationToken);
        mediaSource ??= MediaSourceFactory.Create(); // TODO 临时处理

        if (htmlDocument == null)
        {
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[DLsite] 页面抓取失败: {url}");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[DLsite] 页面抓取成功，开始解析");
        }

        return await this.ScrapeMediaFromHtmlAsync(mediaSource, htmlDocument, progressReporter, cancellationToken);
    }

    private async Task<string?> TryGetUrlByMediaSource(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Log.Debug("尝试获取DLsite资源URL：{MediaSource}", mediaSource.FullPath);

        // 获取文件夹或者文件名
        var mediaName = Path.GetFileNameWithoutExtension(mediaSource.FullPath);

        // 尝试通过文件夹或者文件名获取DLsite资源代码
        var dlsiteCode = DLsiteUtils.TryGetDLsiteCodeByName(mediaName);
        if (dlsiteCode != null)
        {
            Log.Debug("通过文件名获取到DLsite代码：{DLsiteCode}", dlsiteCode);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[DLsite] 从文件名提取到代码: {dlsiteCode}");
            }
            var url = DLsiteUtils.GetUrlByDLsiteCode(dlsiteCode);
            if (url != null)
            {
                // 文件名直接命中产品代码：未走 _dlsiteSearch，需要补一条诊断
                IdentificationDiagnosticsContext.RecordCandidatesDirect(new[]
                {
                    new Models.Tasks.Diagnostics.CandidateDiagnostic
                    {
                        Id = dlsiteCode,
                        Title = mediaName,
                        Url = url,
                        Score = 1.0,
                        SearchKey = dlsiteCode,
                        Chosen = true,
                    }
                });
                IdentificationDiagnosticsContext.MarkChosen(dlsiteCode, mediaName, 1.0);
                return url;
            }
        }
        else
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[DLsite] 文件名未包含有效代码，开始搜索");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 尝试通搜索名字来直接获取URL
        Log.Debug("通过直接搜索名字来获取URL：{MediaName}", mediaName);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 搜索关键词: {mediaName}");
        }
        var searchResult = await _dlsiteSearch.TrySearchUrlsByName(mediaName, cancellationToken);

        if (searchResult.Count == 0)
        {
            Log.Debug("未搜索到相关资源：{MediaName}", mediaName);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[DLsite] 搜索无结果");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 搜索到 {searchResult.Count} 条结果");
        }

        while (searchResult.TryDequeue(out var candidate, out var negPriority))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TODO 这里强制卡分类，后续可以通过配置来选择
            if (candidate.Category.TopCategory == mediaSource.PossibleTopCategory ||
                mediaSource.PossibleTopCategory == TopCategory.Unknown)
            {
                IdentificationDiagnosticsContext.MarkChosen(candidate.Id, candidate.Title, -negPriority);
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[DLsite] 选中候选: {candidate.Id} (得分 {(-negPriority):F3}) {candidate.Title}");
                return candidate.Url;
            }
            else
            {
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[DLsite] 跳过分类不匹配的候选: {candidate.Id} (期望 {mediaSource.PossibleTopCategory}, 实际 {candidate.Category.TopCategory})");
            }
        }

        return null;
    }

    public async Task<HtmlDocument?> GetHtmlDocumentAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        var url = await TryGetUrlByMediaSource(mediaSource, progressReporter, cancellationToken);
        if (url == null)
        {
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 抓取页面: {url}");
        }

        var htmlDocument =
            await _http.Scrape(url, HttpMethod.Get, cancellationToken: cancellationToken);

        return htmlDocument;
    }
}