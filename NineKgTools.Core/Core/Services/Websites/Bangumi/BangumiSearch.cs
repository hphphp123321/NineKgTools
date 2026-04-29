using System.Text.Json;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Utils;
using Serilog;
using System.Net.Http.Headers;

namespace NineKgTools.Core.Services.Websites.Bangumi;

/// <summary>
/// Bangumi搜索功能类，负责所有搜索相关的逻辑
/// </summary>
public class BangumiSearch
{
    private readonly Config _config;
    private readonly HttpService _http;
    private readonly MediaNameSplitterService _splitterService;
    private readonly string _bangumiApiUrl = "https://api.bgm.tv";
    private readonly AuthenticationHeaderValue _authorization;
    
    public BangumiSearch(Config config, HttpService http, MediaNameSplitterService splitterService)
    {
        _config = config;
        _http = http;
        _splitterService = splitterService;
        _authorization = new AuthenticationHeaderValue("Bearer", config.Website.Bangumi.ApiKey);
    }
    
    /// <summary>
    /// 搜索Bangumi条目
    /// </summary>
    public async Task<List<MediaSearchResult>?> SearchSubjects(MediaSource mediaSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 通过文件名搜索条目信息
        var name = Path.GetFileNameWithoutExtension(mediaSource.FullPath);

        // 使用新的关键词提取方法
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);
        IdentificationDiagnosticsContext.RecordKeywords(keywords);
        await IdentificationDiagnosticsContext.DebugAsync(
            $"[Bangumi] 关键词解析: 主={keywords.PrimaryKeyword}" +
            $"{(string.IsNullOrEmpty(keywords.ProductCode) ? "" : $" 产品代码={keywords.ProductCode}")}" +
            $"{(string.IsNullOrEmpty(keywords.CircleName) ? "" : $" 社团={keywords.CircleName}")}" +
            $"{(keywords.SecondaryKeywords?.Count > 0 ? $" 副={string.Join(",", keywords.SecondaryKeywords)}" : "")}");

        // 使用新的搜索策略
        var searchStrategy = new MultiKeywordSearchStrategy();
        var queryBuilder = new SearchQueryBuilder(searchStrategy);
        var queries = queryBuilder.BuildBangumiSearchQueries(keywords);

        var allSearchResults = new List<BangumiSearchResultInstance>();
        var processedQueries = new HashSet<string>(); // 避免重复搜索

        // 按优先级依次尝试搜索查询
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (processedQueries.Contains(query.Query))
                continue;

            processedQueries.Add(query.Query);

            Log.Debug("Bangumi尝试搜索查询: {Query} (类型: {Type}, 优先级: {Priority})",
                query.Query, query.Type, query.Priority);
            await IdentificationDiagnosticsContext.DebugAsync(
                $"[Bangumi] 尝试搜索查询: '{query.Query}' (类型 {query.Type}, 优先级 {query.Priority})");

            var searchResult = await SearchSubjectsByKeyword(query.Query, cancellationToken);

            if (searchResult?.List is { Count: > 0 })
            {
                Log.Information("Bangumi搜索到 {Count} 个结果，查询: {Query}",
                    searchResult.List.Count, query.Query);
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Bangumi] 查询 '{query.Query}' 返回 {searchResult.List.Count} 条原始结果");

                allSearchResults.AddRange(searchResult.List);

                // 如果是高优先级查询且有结果，可以提前结束
                if (query.Priority >= 80 && searchResult.List.Count > 0)
                {
                    Log.Debug("高优先级查询找到结果，提前返回");
                    await IdentificationDiagnosticsContext.DebugAsync(
                        "[Bangumi] 高优先级查询命中，提前结束搜索");
                    break;
                }
            }
            else
            {
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Bangumi] 查询 '{query.Query}' 无结果");
            }

            // 限制搜索次数和结果数量
            if (allSearchResults.Count >= 30 || processedQueries.Count >= 5)
            {
                Log.Debug("已收集足够结果或达到查询次数限制");
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[Bangumi] 已达搜索上限（结果 {allSearchResults.Count} / 查询 {processedQueries.Count}），停止搜索");
                break;
            }
        }

        if (allSearchResults.Count > 0)
        {
            Log.Information("Bangumi总共搜索到 {Count} 个条目", allSearchResults.Count);

            // 转换为MediaSearchResult并使用最佳搜索关键词
            var mediaResults = allSearchResults
                .Select(bangumiResult => bangumiResult.ToMediaSearchResult(keywords.GetBestSearchKeyword()))
                .ToList();

            return mediaResults;
        }

        Log.Warning("Bangumi未搜索到相关条目：{MediaName}", name);
        return null;
    }
    
    /// <summary>
    /// 通过关键字搜索Bangumi条目
    /// </summary>
    public async Task<BangumiSearchResult?> SearchSubjectsByKeyword(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = $"{_bangumiApiUrl}/search/subject/{name}";
        var response = await _http.Get(url, authorization: _authorization, cancellationToken: cancellationToken);
        if (response == null)
        {
            Log.Error("搜索Bangumi条目失败");
            return null;
        }

        try
        {
            var searchResult = JsonSerializer.Deserialize<BangumiSearchResult>(response);
            return searchResult;
        }
        catch (Exception e)
        {
            Log.Error(e, "解析Bangumi搜索结果失败, url: {Url}", url);
            return null;
        }
    }
    
    /// <summary>
    /// 使用新的相关性评分系统生成优先队列
    /// </summary>
    public PriorityQueue<MediaSearchResult, double> GeneratePriorityQueueWithRelevanceScoring(
        List<MediaSearchResult> searchResults,
        MediaKeywords keywords)
    {
        var scorer = new RelevanceScorer();
        var resultQueue = new PriorityQueue<MediaSearchResult, double>();

        // 去重：基于ID去重（Bangumi的ID是唯一的）
        var uniqueResults = searchResults
            .GroupBy(r => r.Id)
            .Select(g => g.First())
            .ToList();

        var skippedCount = 0;
        foreach (var result in uniqueResults)
        {
            // 计算相关性得分
            var relevanceScore = scorer.CalculateRelevance(result, keywords);

            // 如果得分过低，跳过
            if (relevanceScore < _config.Identification.MinSimilarity)
            {
                skippedCount++;
                Log.Debug("Bangumi搜索结果相关性过低，跳过: {Title} (得分: {Score})",
                    result.Title, relevanceScore);
                continue;
            }

            // 使用负分数作为优先级（PriorityQueue是最小堆）
            resultQueue.Enqueue(result, -relevanceScore);

            Log.Debug("Bangumi搜索结果入队: {Title}, 相关性得分: {Score:F3}",
                result.Title, relevanceScore);
        }

        // 同步把汇总写到任务日志
        _ = IdentificationDiagnosticsContext.DebugAsync(
            $"[Bangumi] 相关性过滤完成: 唯一候选 {uniqueResults.Count}，过滤（< {_config.Identification.MinSimilarity}）{skippedCount}，入队 {resultQueue.Count}");

        if (resultQueue.Count > 0)
        {
            var preview = resultQueue.UnorderedItems
                .OrderBy(p => p.Priority)
                .Take(3)
                .Select(p => $"{-p.Priority:F3}→{p.Element.Title}")
                .ToList();
            _ = IdentificationDiagnosticsContext.DebugAsync(
                $"[Bangumi] 入队 Top {preview.Count}: {string.Join(" | ", preview)}");
        }

        IdentificationDiagnosticsContext.RecordCandidates(resultQueue, uniqueResults.Count, skippedCount);

        return resultQueue;
    }

    /// <summary>
    /// 根据名称匹配最佳的条目ID
    /// </summary>
    public async Task<int> MatchSubjectIdByName(List<MediaSearchResult> searchResultList, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 提取关键词用于评分
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);

        // 使用新的相关性评分系统
        var pq = GeneratePriorityQueueWithRelevanceScoring(searchResultList, keywords);

        if (!pq.TryPeek(out var max, out var negPriority))
        {
            await IdentificationDiagnosticsContext.DebugAsync("[Bangumi] 评分后无候选可选");
            return 0;
        }

        IdentificationDiagnosticsContext.MarkChosen(max.Id, max.Title, -negPriority);
        await IdentificationDiagnosticsContext.DebugAsync(
            $"[Bangumi] 选中候选: ID={max.Id} (得分 {(-negPriority):F3}) {max.Title}");

        // 匹配name
        var maxId = int.Parse(max.Id);
        return maxId;
    }
}