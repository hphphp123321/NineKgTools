using System;
using System.Collections.Generic;

namespace NineKgTools.Core.Models.Tasks.Diagnostics;

/// <summary>
/// 单次"调用某个 IWebsite 试图识别"的诊断记录。
/// </summary>
public class WebsiteAttemptDiagnostic
{
    public string WebsiteName { get; set; } = string.Empty;

    public WebsiteAttemptStatus Status { get; set; } = WebsiteAttemptStatus.NoMatch;

    /// <summary>
    /// 这次尝试的来源：通过 ID 直查 / 通过搜索 / 命中缓存。
    /// </summary>
    public WebsiteAttemptSource Source { get; set; } = WebsiteAttemptSource.Search;

    /// <summary>
    /// 当 <see cref="Source"/> = ById 时使用的网站特定 ID。
    /// </summary>
    public string? AttemptedId { get; set; }

    /// <summary>
    /// 跳过、未匹配、异常时给前端看的具体原因（不含敏感信息）。
    /// </summary>
    public string? Reason { get; set; }

    public DateTime StartTime { get; set; }
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// 网站搜索阶段返回的"原始候选总数"（未做相似度过滤前）。
    /// </summary>
    public int TotalCandidatesScanned { get; set; }

    /// <summary>
    /// 因相关性低于 <c>identification.min_similarity</c> 被丢弃的候选数。
    /// </summary>
    public int FilteredByMinSimilarityCount { get; set; }

    /// <summary>
    /// 按相关性降序排列的 Top N 候选（默认 5 条）。命中那条 <see cref="CandidateDiagnostic.Chosen"/> 为 true。
    /// </summary>
    public List<CandidateDiagnostic> TopCandidates { get; set; } = new();

    /// <summary>
    /// 命中时网站返回的最终媒体的网站特定 ID（DLsite 的 RJ 号、Bangumi/Steam 的整数 ID）。
    /// </summary>
    public string? ResultId { get; set; }
    public string? ResultTitle { get; set; }

    /// <summary>
    /// 命中候选的相关性得分（0-1）；ById/CacheHit 通常没有得分。
    /// </summary>
    public double? ResultScore { get; set; }
}

public enum WebsiteAttemptStatus
{
    /// <summary>命中，<see cref="WebsiteAttemptDiagnostic.ResultId"/> / Title 已填充。</summary>
    Success,

    /// <summary>调用成功但没有任何候选 / 候选都被过滤。</summary>
    NoMatch,

    /// <summary>分类不支持等原因，根本没真正调用网站。</summary>
    Skipped,

    /// <summary>HTTP 超时、DOM 结构变动等异常被 SafeInvoke 吞掉。</summary>
    Exception,

    /// <summary>从识别缓存直接返回，未真正访问网站。</summary>
    CacheHit
}

public enum WebsiteAttemptSource
{
    /// <summary>通过 IWebsite.GetMediaInfoAsync(MediaSource) 自动搜索 + 选最佳。</summary>
    Search,

    /// <summary>通过 IWebsite.GetMediaInfoAsync(string id, ...) 用网站特定 ID 直查。</summary>
    ById,

    /// <summary>从 IdentificationCacheService 命中。</summary>
    Cache
}
