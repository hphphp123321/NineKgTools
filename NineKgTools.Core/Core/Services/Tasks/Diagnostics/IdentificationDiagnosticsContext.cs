using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites;
using NineKgTools.Utils;

namespace NineKgTools.Core.Services.Tasks.Diagnostics;

/// <summary>
/// 通过 <see cref="AsyncLocal{T}"/> 在单个识别任务执行期间提供"诊断收集器"。
/// 不修改任何 IWebsite / IProgressReporter 接口签名，所有上报都是 <c>Current?.RecordX(...)</c> 调用，
/// 上下文外（非任务驱动的网站调用）不会产生任何副作用。
/// </summary>
public static class IdentificationDiagnosticsContext
{
    private static readonly AsyncLocal<IdentificationDiagnostics?> _current = new();

    /// <summary>当前任务的诊断收集器；非任务上下文中为 null。</summary>
    public static IdentificationDiagnostics? Current => _current.Value;

    /// <summary>
    /// 在 <see cref="SingleSourceIdentificationTask"/> 入口开启作用域，using 释放时自动还原。
    /// </summary>
    public static IDisposable BeginScope(IdentificationDiagnostics diagnostics)
    {
        var previous = _current.Value;
        _current.Value = diagnostics;
        return new ScopeReleaser(() => _current.Value = previous);
    }

    private sealed class ScopeReleaser : IDisposable
    {
        private Action? _onDispose;
        public ScopeReleaser(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            var action = Interlocked.Exchange(ref _onDispose, null);
            action?.Invoke();
        }
    }

    // --------- 关键词上报（首站为准） ---------

    public static void RecordKeywords(MediaKeywords? keywords)
    {
        if (keywords == null) return;
        var ctx = Current;
        if (ctx == null || ctx.Keywords != null) return;

        ctx.Keywords = new IdentificationKeywordsSnapshot
        {
            PrimaryKeyword = keywords.PrimaryKeyword ?? string.Empty,
            SecondaryKeywords = keywords.SecondaryKeywords?.ToList() ?? new List<string>(),
            ProductCode = keywords.ProductCode,
            CircleName = keywords.CircleName,
            CleanedTitle = keywords.CleanedTitle ?? string.Empty,
            DetectedLanguage = keywords.DetectedLanguage.ToString(),
            Version = keywords.Version,
            Date = keywords.Date,
        };
    }

    // --------- 网站尝试上报 ---------

    public static WebsiteAttemptDiagnostic? BeginAttempt(
        string websiteName,
        WebsiteAttemptSource source,
        string? attemptedId = null)
    {
        var ctx = Current;
        if (ctx == null) return null;

        var attempt = new WebsiteAttemptDiagnostic
        {
            WebsiteName = websiteName,
            Source = source,
            AttemptedId = attemptedId,
            StartTime = DateTime.UtcNow,
        };
        ctx.WebsiteAttempts.Add(attempt);
        ctx.CurrentAttempt = attempt;
        return attempt;
    }

    /// <summary>
    /// 在 SafeInvokeWebsiteAsync 调用结束后调用：根据返回媒体 / 异常 / null，定型 attempt。
    /// 注意：网站特定 ID / 得分应由实现类通过 <see cref="MarkChosen"/> 显式上报；
    /// 本方法只在调用方未上报、且 media 非 null 时退化用 media.Title 作为 ResultTitle。
    /// </summary>
    public static void EndAttempt(
        WebsiteAttemptDiagnostic? attempt,
        Models.Media.MediaBase? media,
        Exception? exception,
        string? noMatchReason = null)
    {
        if (attempt == null) return;
        var ctx = Current;
        attempt.Duration = DateTime.UtcNow - attempt.StartTime;

        if (exception != null)
        {
            attempt.Status = WebsiteAttemptStatus.Exception;
            attempt.Reason = exception.Message;
        }
        else if (media != null)
        {
            attempt.Status = WebsiteAttemptStatus.Success;
            // 若 IWebsite 实现没显式调 MarkChosen，则至少把命中标题填上
            if (string.IsNullOrEmpty(attempt.ResultTitle))
                attempt.ResultTitle = media.Title;
            // 注意：FinalChoice 是计算属性（找 Status==Success/CacheHit 的第一条），无需在此手动赋值
        }
        else
        {
            attempt.Status = WebsiteAttemptStatus.NoMatch;
            attempt.Reason = noMatchReason;
        }

        if (ctx != null && ReferenceEquals(ctx.CurrentAttempt, attempt))
        {
            ctx.CurrentAttempt = null;
        }
    }

    /// <summary>
    /// IWebsite 实现在选定一个候选后调用：把网站特定 ID + 标题 + 得分写到当前 attempt，
    /// 并在 TopCandidates 里把对应那条标记为 Chosen=true（前端高亮）。
    /// </summary>
    public static void MarkChosen(string websiteSpecificId, string? title = null, double? score = null)
    {
        var attempt = Current?.CurrentAttempt;
        if (attempt == null) return;

        attempt.ResultId = websiteSpecificId;
        if (!string.IsNullOrEmpty(title)) attempt.ResultTitle = title;
        if (score.HasValue) attempt.ResultScore = score;

        var match = attempt.TopCandidates.FirstOrDefault(c => c.Id == websiteSpecificId);
        if (match != null)
        {
            match.Chosen = true;
            if (!score.HasValue) attempt.ResultScore = match.Score;
        }
    }

    // --------- 跳过/缓存命中（不走 SafeInvoke 的快捷路径） ---------

    public static void RecordSkippedWebsite(string websiteName, string reason)
    {
        var ctx = Current;
        if (ctx == null) return;
        ctx.WebsiteAttempts.Add(new WebsiteAttemptDiagnostic
        {
            WebsiteName = websiteName,
            Source = WebsiteAttemptSource.Search,
            Status = WebsiteAttemptStatus.Skipped,
            Reason = reason,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
        });
    }

    public static void RecordCacheHit(string websiteName, string websiteId, Models.Media.MediaBase media)
    {
        var ctx = Current;
        if (ctx == null) return;
        var attempt = new WebsiteAttemptDiagnostic
        {
            WebsiteName = websiteName,
            Source = WebsiteAttemptSource.Cache,
            Status = WebsiteAttemptStatus.CacheHit,
            AttemptedId = websiteId,
            StartTime = DateTime.UtcNow,
            Duration = TimeSpan.Zero,
            ResultId = media.Id != 0 ? media.Id.ToString() : websiteId,
            ResultTitle = media.Title,
        };
        ctx.WebsiteAttempts.Add(attempt);
        // FinalChoice 自动通过计算属性命中
    }

    // --------- 搜索候选上报（由网站实现在 SearchMediaAsync 之后调用） ---------

    /// <summary>
    /// 上报当前网站的搜索候选：
    /// 把 PriorityQueue 反射快照（不破坏原 queue），按 -priority 倒序取 Top N（默认 5）填充到当前 attempt。
    /// </summary>
    public static void RecordCandidates(
        PriorityQueue<MediaSearchResult, double> queue,
        int totalScanned,
        int filteredCount,
        int topN = 5)
    {
        var ctx = Current;
        var attempt = ctx?.CurrentAttempt;
        if (attempt == null) return;

        attempt.TotalCandidatesScanned = totalScanned;
        attempt.FilteredByMinSimilarityCount = filteredCount;

        // queue 内的优先级是 -relevanceScore（最小堆，分数越大越优先），快照后按 priority 升序 = 相关性降序
        var snapshot = queue.UnorderedItems
            .OrderBy(p => p.Priority)
            .Take(topN)
            .Select(p => new CandidateDiagnostic
            {
                Id = p.Element.Id,
                Title = p.Element.Title,
                Url = p.Element.Url,
                Score = -p.Priority,
                SearchKey = p.Element.SearchKey,
                Chosen = false,
            })
            .ToList();

        attempt.TopCandidates = snapshot;
    }

    /// <summary>
    /// 直接上报候选（已经知道每条得分的场景，比如 DLsite 用产品代码直查命中）。
    /// </summary>
    public static void RecordCandidatesDirect(IEnumerable<CandidateDiagnostic> candidates)
    {
        var attempt = Current?.CurrentAttempt;
        if (attempt == null) return;
        attempt.TopCandidates = candidates.Take(5).ToList();
    }

    // --------- 任务日志快捷写入（让 Search 类等"深处代码"无需 IProgressReporter 参数即可写日志） ---------

    /// <summary>
    /// 通过当前作用域绑定的 IProgressReporter 写一条 Debug 日志；
    /// 上下文外（无 SingleSourceIdentificationTask 包裹）静默丢弃。
    /// </summary>
    public static Task DebugAsync(string message, string? currentItem = null)
    {
        var reporter = Current?.Reporter;
        return reporter != null ? reporter.DebugAsync(message, currentItem) : Task.CompletedTask;
    }

    public static Task InfoAsync(string message, double? progress = null, string? currentItem = null)
    {
        var reporter = Current?.Reporter;
        return reporter != null
            ? reporter.ReportAsync(message, progress, TaskLogLevel.Info, currentItem)
            : Task.CompletedTask;
    }

    public static Task WarningAsync(string message, string? currentItem = null)
    {
        var reporter = Current?.Reporter;
        return reporter != null ? reporter.WarningAsync(message, null, currentItem) : Task.CompletedTask;
    }
}
