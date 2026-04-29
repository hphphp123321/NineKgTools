using System.Text.Json;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Core.Services.Websites.Steam.Models;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Steam;

/// <summary>
/// Steam搜索功能类，负责根据关键词搜索Steam游戏
/// </summary>
public class SteamSearch
{
    private readonly Config _config;
    private readonly HttpService _http;
    private readonly MediaNameSplitterService _splitterService;

    private const string SearchEndpoint = "https://steamcommunity.com/actions/SearchApps/";

    public SteamSearch(Config config, HttpService http, MediaNameSplitterService splitterService)
    {
        _config = config;
        _http = http;
        _splitterService = splitterService;
    }

    /// <summary>
    /// 根据媒体源执行搜索，返回转换后的 MediaSearchResult 列表
    /// </summary>
    public async Task<List<MediaSearchResult>?> SearchAppsAsync(MediaSource mediaSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var name = Path.GetFileNameWithoutExtension(mediaSource.FullPath);
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);
        IdentificationDiagnosticsContext.RecordKeywords(keywords);
        await IdentificationDiagnosticsContext.DebugAsync(
            $"[Steam] 关键词解析: 主={keywords.PrimaryKeyword}" +
            $"{(string.IsNullOrEmpty(keywords.ProductCode) ? "" : $" 产品代码={keywords.ProductCode}")}" +
            $"{(string.IsNullOrEmpty(keywords.CircleName) ? "" : $" 社团={keywords.CircleName}")}" +
            $"{(keywords.SecondaryKeywords?.Count > 0 ? $" 副={string.Join(",", keywords.SecondaryKeywords)}" : "")}");

        var searchStrategy = new MultiKeywordSearchStrategy();
        var queryBuilder = new SearchQueryBuilder(searchStrategy);

        // Steam 搜索用纯文本即可，不需要结构化查询，复用 Bangumi 的关键词构造策略取最佳关键词集合
        var queries = queryBuilder.BuildBangumiSearchQueries(keywords);

        var allResults = new List<SteamSearchAppResult>();
        var processedQueries = new HashSet<string>();
        var bestSearchKey = keywords.GetBestSearchKeyword();

        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!processedQueries.Add(query.Query)) continue;

            Log.Debug("Steam尝试搜索查询: {Query} (优先级: {Priority})", query.Query, query.Priority);
            await IdentificationDiagnosticsContext.DebugAsync(
                $"[Steam] 尝试搜索查询: '{query.Query}' (优先级 {query.Priority})");

            var results = await SearchAppsByKeyword(query.Query, cancellationToken);
            if (results is { Count: > 0 })
            {
                Log.Information("Steam搜索到 {Count} 个结果，查询: {Query}", results.Count, query.Query);
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Steam] 查询 '{query.Query}' 返回 {results.Count} 条原始结果");
                allResults.AddRange(results);

                if (query.Priority >= 80)
                {
                    Log.Debug("高优先级查询命中，提前返回");
                    await IdentificationDiagnosticsContext.DebugAsync(
                        "[Steam] 高优先级查询命中，提前结束搜索");
                    break;
                }
            }
            else
            {
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Steam] 查询 '{query.Query}' 无结果");
            }

            if (allResults.Count >= 30 || processedQueries.Count >= 5)
            {
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Steam] 已达搜索上限（结果 {allResults.Count} / 查询 {processedQueries.Count}），停止搜索");
                break;
            }
        }

        if (allResults.Count == 0)
        {
            Log.Warning("Steam未搜索到相关条目：{MediaName}", name);
            return null;
        }

        // 去重 + 转为 MediaSearchResult
        var mediaResults = allResults
            .GroupBy(r => r.Appid)
            .Select(g => g.First())
            .Select(r => ToMediaSearchResult(r, bestSearchKey))
            .ToList();

        return mediaResults;
    }

    /// <summary>
    /// 通过关键字调用 Steam Community 的 SearchApps 接口
    /// </summary>
    public async Task<List<SteamSearchAppResult>?> SearchAppsByKeyword(string query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = SearchEndpoint + Uri.EscapeDataString(query);
        var response = await _http.Get(url, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(response))
        {
            Log.Warning("Steam搜索请求无响应: {Url}", url);
            return null;
        }

        try
        {
            var results = JsonSerializer.Deserialize<List<SteamSearchAppResult>>(response);
            return results;
        }
        catch (Exception e)
        {
            Log.Error(e, "解析Steam搜索结果失败, url: {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// 使用相关性评分系统生成优先队列
    /// </summary>
    public PriorityQueue<MediaSearchResult, double> GeneratePriorityQueueWithRelevanceScoring(
        List<MediaSearchResult> searchResults,
        MediaKeywords keywords)
    {
        var scorer = new RelevanceScorer();
        var resultQueue = new PriorityQueue<MediaSearchResult, double>();

        var uniqueResults = searchResults
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();

        var skippedCount = 0;
        foreach (var result in uniqueResults)
        {
            var relevanceScore = scorer.CalculateRelevance(result, keywords);

            if (relevanceScore < _config.Identification.MinSimilarity)
            {
                skippedCount++;
                Log.Debug("Steam搜索结果相关性过低，跳过: {Title} (得分: {Score})", result.Title, relevanceScore);
                continue;
            }

            // PriorityQueue 是最小堆，用负分让高分优先出队
            resultQueue.Enqueue(result, -relevanceScore);
            Log.Debug("Steam搜索结果入队: {Title}, 得分: {Score:F3}", result.Title, relevanceScore);
        }

        // 同步把汇总写到任务日志
        _ = IdentificationDiagnosticsContext.DebugAsync(
            $"[Steam] 相关性过滤完成: 唯一候选 {uniqueResults.Count}，过滤（< {_config.Identification.MinSimilarity}）{skippedCount}，入队 {resultQueue.Count}");

        if (resultQueue.Count > 0)
        {
            var preview = resultQueue.UnorderedItems
                .OrderBy(p => p.Priority)
                .Take(3)
                .Select(p => $"{-p.Priority:F3}→{p.Element.Title}")
                .ToList();
            _ = IdentificationDiagnosticsContext.DebugAsync(
                $"[Steam] 入队 Top {preview.Count}: {string.Join(" | ", preview)}");
        }

        IdentificationDiagnosticsContext.RecordCandidates(resultQueue, uniqueResults.Count, skippedCount);

        return resultQueue;
    }

    /// <summary>
    /// 匹配最佳的 appid
    /// </summary>
    public async Task<int> MatchAppIdByName(List<MediaSearchResult> searchResultList, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);
        var pq = GeneratePriorityQueueWithRelevanceScoring(searchResultList, keywords);

        if (!pq.TryPeek(out var top, out var negPriority))
        {
            await IdentificationDiagnosticsContext.DebugAsync("[Steam] 评分后无候选可选");
            return 0;
        }
        IdentificationDiagnosticsContext.MarkChosen(top.Id, top.Title, -negPriority);
        await IdentificationDiagnosticsContext.DebugAsync(
            $"[Steam] 选中候选: AppID={top.Id} (得分 {(-negPriority):F3}) {top.Title}");
        return int.TryParse(top.Id, out var appId) ? appId : 0;
    }

    private static MediaSearchResult ToMediaSearchResult(SteamSearchAppResult src, string searchKey)
    {
        return new MediaSearchResult
        {
            Id = src.Appid,
            SearchKey = searchKey,
            Title = src.Name,
            Url = $"https://store.steampowered.com/app/{src.Appid}",
            Category = StaticCategories.OtherGame,
            Poster = string.IsNullOrEmpty(src.Logo) ? null : new Image(new Uri(src.Logo))
        };
    }
}
