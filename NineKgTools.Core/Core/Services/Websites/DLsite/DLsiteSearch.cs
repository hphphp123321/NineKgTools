using HtmlAgilityPack;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

/// <summary>
/// DLsite搜索功能类，负责所有搜索相关的逻辑
/// </summary>
public class DLsiteSearch
{
    private readonly Config _config;
    private readonly HttpService _http;
    private readonly DLsiteService _dlsiteService;
    private readonly MediaNameSplitterService _splitterService;
    
    public DLsiteSearch(Config config, HttpService http, DLsiteService dlsiteService, MediaNameSplitterService splitterService)
    {
        _config = config;
        _http = http;
        _dlsiteService = dlsiteService;
        _splitterService = splitterService;
    }
    
    /// <summary>
    /// 通过名称搜索DLsite资源
    /// </summary>
    public async Task<PriorityQueue<MediaSearchResult, double>> TrySearchUrlsByName(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 使用新的关键词提取方法
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);
        IdentificationDiagnosticsContext.RecordKeywords(keywords);
        await IdentificationDiagnosticsContext.DebugAsync(
            $"[DLsite] 关键词解析: 主={keywords.PrimaryKeyword}" +
            $"{(string.IsNullOrEmpty(keywords.ProductCode) ? "" : $" 产品代码={keywords.ProductCode}")}" +
            $"{(string.IsNullOrEmpty(keywords.CircleName) ? "" : $" 社团={keywords.CircleName}")}" +
            $"{(keywords.SecondaryKeywords?.Count > 0 ? $" 副={string.Join(",", keywords.SecondaryKeywords)}" : "")}");

        // 如果有产品代码，尝试直接构建URL
        if (!string.IsNullOrEmpty(keywords.ProductCode))
        {
            var directUrl = DLsiteUtils.GetUrlByDLsiteCode(keywords.ProductCode);
            if (!string.IsNullOrEmpty(directUrl))
            {
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[DLsite] 通过产品代码 {keywords.ProductCode} 直接构造 URL，跳过关键词搜索");
                var directResult = new MediaSearchResult
                {
                    SearchKey = keywords.ProductCode,
                    Title = name,
                    Url = directUrl,
                    Id = keywords.ProductCode
                };
                var queue = new PriorityQueue<MediaSearchResult, double>();
                queue.Enqueue(directResult, -1.0); // 精确匹配，最高优先级（负分越小越先出队）
                IdentificationDiagnosticsContext.RecordCandidates(queue, totalScanned: 1, filteredCount: 0);
                return queue;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        // 使用新的搜索策略
        var searchStrategy = new MultiKeywordSearchStrategy();
        var queries = searchStrategy.GenerateSearchQueries(keywords, separator:"+");

        var allSearchResults = new List<MediaSearchResult>();
        var processedQueries = new HashSet<string>(); // 避免重复搜索

        // 按优先级依次尝试搜索查询
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (processedQueries.Contains(query.Query))
                continue;

            processedQueries.Add(query.Query);

            Log.Debug("尝试搜索查询: {Query} (类型: {Type}, 优先级: {Priority})",
                query.Query, query.Type, query.Priority);
            await IdentificationDiagnosticsContext.DebugAsync(
                $"[DLsite] 尝试搜索查询: '{query.Query}' (类型 {query.Type}, 优先级 {query.Priority})");

            var searchList = await SearchUrlsByQuery(query.Query, cancellationToken);
            await IdentificationDiagnosticsContext.DebugAsync(
                $"[DLsite] 查询 '{query.Query}' 返回 {searchList.Count} 条原始结果");

            // 为每个结果添加查询信息，用于后续评分
            foreach (var result in searchList)
            {
                result.SearchKey = query.Query; // 更新搜索关键词为实际使用的查询
            }

            allSearchResults.AddRange(searchList);

            // 如果是高优先级查询且有结果，可以提前返回
            if (query.Priority >= 80 && searchList.Count > 0)
            {
                Log.Debug("高优先级查询找到 {Count} 个结果，提前返回", searchList.Count);
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[DLsite] 高优先级查询命中 {searchList.Count} 条，提前结束搜索");
                break;
            }

            // 限制搜索次数，避免过多API调用
            if (allSearchResults.Count >= 30 || processedQueries.Count >= 5)
            {
                Log.Debug("已收集足够结果或达到查询次数限制");
                await IdentificationDiagnosticsContext.DebugAsync(
                    $"[DLsite] 已达搜索上限（结果 {allSearchResults.Count} / 查询 {processedQueries.Count}），停止搜索");
                break;
            }
        }

        // 使用新的评分系统生成优先队列
        return GeneratePriorityQueueWithRelevanceScoring(allSearchResults, keywords);
    }
    
    /// <summary>
    /// 通过搜索查询获取搜索结果
    /// </summary>
    private async Task<List<MediaSearchResult>> SearchUrlsByQuery(string query, CancellationToken cancellationToken = default)
    {
        var resultList = new List<MediaSearchResult>();
        // dlsite query中不能包含" "
        query = query.Replace(" ", "+");

        foreach (var type in (string[]) ["maniax", "books", "pro"])
        {
            cancellationToken.ThrowIfCancellationRequested();

            var baseUrl =
                $"https://www.dlsite.com/{type}/fsr/=/keyword/{query}/trend/options_and_or/and/per_page/30/page/1/from/fs.header";
            DLsiteUtils.AddUrlLocaleSuffix(ref baseUrl, DLsiteService.CnLang);

            var htmlDocument = await _http.Scrape(baseUrl, HttpMethod.Get, cancellationToken: cancellationToken);
            if (htmlDocument == null)
            {
                return resultList;
            }

            var worksNodes =
                htmlDocument.DocumentNode.SelectNodes("//ul[@id='search_result_img_box' and @class='n_worklist']/li");
            if (worksNodes == null) return resultList;

            foreach (var workNode in worksNodes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var titleNode = workNode.SelectSingleNode(".//div[@class='multiline_truncate']/a");
                if (titleNode == null) continue;
                var url = titleNode.GetAttributeValue("href", "");
                DLsiteUtils.AddUrlLocaleSuffix(ref url, DLsiteService.CnLang); // 添加中文语言后缀

                // 判断url是否在resultList中
                if (resultList.Any(r => r.Url == url))
                {
                    continue;
                }

                var categoryNode = workNode.SelectSingleNode(".//dd[@class='work_category_free_sample']");
                var categoryText = categoryNode?.InnerText.Trim();
                var category = _dlsiteService.GetCategoryByText(categoryText);

                var posterNode = workNode.SelectSingleNode(".//img[@ref='popup_img']");
                var posterUrl = posterNode?.GetAttributeValue("src", "");
                var poster = posterUrl != null ? _dlsiteService.GetImageByUrl(posterUrl) : null;

                var title = _dlsiteService.TrimTitle(titleNode.InnerText.Trim());

                var searchResult = new MediaSearchResult
                {
                    SearchKey = query,
                    Title = title,
                    Url = url,
                    Id = DLsiteUtils.TryGetDLsiteCodeByName(url)!,
                    Category = category,
                    Poster = poster,
                };
                resultList.Add(searchResult);
            }
        }

        return resultList;
    }
    
    /// <summary>
    /// 使用新的相关性评分系统生成优先队列
    /// </summary>
    private PriorityQueue<MediaSearchResult, double> GeneratePriorityQueueWithRelevanceScoring(
        List<MediaSearchResult> searchResults, 
        MediaKeywords keywords)
    {
        var scorer = new RelevanceScorer();
        var resultQueue = new PriorityQueue<MediaSearchResult, double>();
        
        // 去重：基于URL去重
        var uniqueResults = searchResults
            .GroupBy(r => r.Url)
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
                continue;
            }

            // 使用负分数作为优先级（PriorityQueue是最小堆）
            resultQueue.Enqueue(result, -relevanceScore);
        }

        // 汇总日志：把每条结果逐行 Debug 改成一行摘要，避免识别时刷屏
        Log.Debug("DLsite 相关性过滤完成: 总数 {Total}，跳过（相关性 < {MinSimilarity}）{Skipped}，入队 {Enqueued}",
            uniqueResults.Count, _config.Identification.MinSimilarity, skippedCount, resultQueue.Count);

        // 同步把汇总写到任务日志，让前端「执行日志」也看得到这条关键决策
        _ = IdentificationDiagnosticsContext.DebugAsync(
            $"[DLsite] 相关性过滤完成: 唯一候选 {uniqueResults.Count}，过滤（< {_config.Identification.MinSimilarity}）{skippedCount}，入队 {resultQueue.Count}");

        // 入队前 Top 候选预览（最多 3 条，避免日志爆炸）
        if (resultQueue.Count > 0)
        {
            var preview = resultQueue.UnorderedItems
                .OrderBy(p => p.Priority)
                .Take(3)
                .Select(p => $"{-p.Priority:F3}→{p.Element.Title}")
                .ToList();
            _ = IdentificationDiagnosticsContext.DebugAsync(
                $"[DLsite] 入队 Top {preview.Count}: {string.Join(" | ", preview)}");
        }

        IdentificationDiagnosticsContext.RecordCandidates(resultQueue, uniqueResults.Count, skippedCount);

        return resultQueue;
    }
}