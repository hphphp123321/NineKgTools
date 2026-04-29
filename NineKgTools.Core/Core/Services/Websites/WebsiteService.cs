using NineKgTools.Core.Models.Cache;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Cache;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Websites;

public class WebsiteService
{
    private Config _config;
    private readonly IdentificationCacheService _cacheService;

    public Dictionary<string, IWebsite> WebsiteNameMap { get; } = new();

    public Dictionary<TopCategory, List<IWebsite>> Websites { get; } = new()
    {
        { TopCategory.Unknown, new List<IWebsite>() },
        { TopCategory.Audio, new List<IWebsite>() },
        { TopCategory.Video, new List<IWebsite>() },
        { TopCategory.Game, new List<IWebsite>() },
        { TopCategory.Text, new List<IWebsite>() },
        { TopCategory.Picture, new List<IWebsite>() },
    };

    public WebsiteService(Config config, IEnumerable<IWebsite> websites, IdentificationCacheService cacheService)
    {
        _config = config;
        _cacheService = cacheService;

        foreach (var website in websites)
        {
            // 将网站名和网站对象映射
            WebsiteNameMap.TryAdd(website.Name, website);
        }

        // 按照设置给网站优先级排序
        foreach (var websiteString in config.Website.Priority.Unknown)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            Websites[TopCategory.Unknown].Add(website);
        }

        foreach (var websiteString in config.Website.Priority.Audio)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            if (!website.TopCategories.Contains(TopCategory.Audio))
            {
                Log.Warning("网站{WebsiteName}不包含音频分类", website.Name);
            }

            Websites[TopCategory.Audio].Add(website);
        }
        
        foreach (var websiteString in config.Website.Priority.Video)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            if (!website.TopCategories.Contains(TopCategory.Video))
            {
                Log.Warning("网站{WebsiteName}不包含视频分类", website.Name);
            }

            Websites[TopCategory.Video].Add(website);
        }
        
        foreach (var websiteString in config.Website.Priority.Game)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            if (!website.TopCategories.Contains(TopCategory.Game))
            {
                Log.Warning("网站{WebsiteName}不包含游戏分类", website.Name);
            }

            Websites[TopCategory.Game].Add(website);
        }
        
        foreach (var websiteString in config.Website.Priority.Text)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            if (!website.TopCategories.Contains(TopCategory.Text))
            {
                Log.Warning("网站{WebsiteName}不包含文本分类", website.Name);
            }

            Websites[TopCategory.Text].Add(website);
        }
        
        foreach (var websiteString in config.Website.Priority.Picture)
        {
            if (!WebsiteNameMap.TryGetValue(websiteString, out var website))
            {
                Log.Warning("未找到网站：{WebsiteString}", websiteString);
            }

            if (!website.TopCategories.Contains(TopCategory.Picture))
            {
                Log.Warning("网站{WebsiteName}不包含图片分类", website.Name);
            }

            Websites[TopCategory.Picture].Add(website);
        }
    }

    public void LogWebsites()
    {
        foreach (var (topCategory, websiteList) in Websites)
        {
            var websiteNames = websiteList.Select(website => website.Name).ToList();
            Log.Debug("顶层分类：{TopCategory}，网站：{WebsiteNames}", topCategory, websiteNames);
        }
    }

    /// <summary>
    /// 获取媒体信息的对外接口
    /// </summary>
    /// <param name="mediaSource">包装好的媒体源</param>
    /// <returns></returns>
    public async Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource)
    {
        return await GetMediaInfoAsync(mediaSource, null, null, CancellationToken.None);
    }

    /// <summary>
    /// 获取媒体信息的对外接口（支持识别选项）
    /// </summary>
    /// <param name="mediaSource">包装好的媒体源</param>
    /// <param name="options">识别选项</param>
    /// <param name="progressReporter">进度报告器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public async Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource, IdentificationOptions? options, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        Log.Information("准备爬取媒体信息：{MediaSourceFullPath}", mediaSource.FullPath);

        // 检查是否已取消
        cancellationToken.ThrowIfCancellationRequested();

        // 如果有识别选项并且指定了网站，仅使用该网站进行识别
        if (options?.PreferredWebsite != null)
        {
            Log.Information("使用识别选项指定的网站（仅此网站）: {Website}", options.PreferredWebsite);
            if (progressReporter != null)
            {
                await progressReporter.ReportAsync($"使用指定网站: {options.PreferredWebsite}");
            }

            if (WebsiteNameMap.TryGetValue(options.PreferredWebsite, out var preferredWebsite) && preferredWebsite.Enable)
            {
                // 如果有网站特定ID，尝试直接使用ID获取
                var websiteId = options.GetWebsiteId(options.PreferredWebsite);
                if (!string.IsNullOrEmpty(websiteId))
                {
                    // 检查缓存（如果不是跳过缓存或强制刷新）
                    if (options?.Strategy != IdentificationStrategy.ForceRefresh &&
                        !options?.SkipCache == true)
                    {
                        var cached = await _cacheService.GetAsync(options.PreferredWebsite, websiteId, options);
                        if (cached != null)
                        {
                            Log.Information("从缓存获取媒体信息：{MediaTitle}", cached.Title);
                            if (progressReporter != null)
                            {
                                await progressReporter.ReportAsync($"从缓存获取: {cached.Title}");
                            }
                            IdentificationDiagnosticsContext.RecordCacheHit(options.PreferredWebsite, websiteId, cached);
                            return cached;
                        }
                    }

                    // 如果是仅缓存模式且没有缓存，返回null
                    if (options?.Strategy == IdentificationStrategy.CacheOnly)
                    {
                        Log.Warning("仅缓存模式下未找到缓存: {Website} - {Id}", options.PreferredWebsite, websiteId);
                        if (progressReporter != null)
                        {
                            await progressReporter.WarningAsync($"仅缓存模式下未找到缓存: {options.PreferredWebsite} - {websiteId}");
                        }
                        if (IdentificationDiagnosticsContext.Current is { } ctxCacheOnly)
                        {
                            ctxCacheOnly.OverallFailureReason = $"仅缓存模式下未找到缓存: {options.PreferredWebsite} - {websiteId}";
                        }
                        return null;
                    }

                    Log.Debug("尝试使用网站特定ID: {Id}", websiteId);
                    if (progressReporter != null)
                    {
                        await progressReporter.DebugAsync($"尝试使用网站特定ID: {websiteId}");
                    }
                    var preferredByIdAttempt = IdentificationDiagnosticsContext.BeginAttempt(
                        preferredWebsite.Name, WebsiteAttemptSource.ById, websiteId);
                    var media = await SafeInvokeWebsiteAsync(
                        preferredWebsite.Name,
                        () => preferredWebsite.GetMediaInfoAsync(websiteId, mediaSource, progressReporter, cancellationToken),
                        progressReporter,
                        cancellationToken,
                        preferredByIdAttempt);
                    if (media != null)
                    {
                        Log.Information("使用网站特定ID成功获取媒体信息：{MediaTitle}", media.Title);
                        if (progressReporter != null)
                        {
                            await progressReporter.SuccessAsync($"使用网站特定ID成功: {media.Title}");
                        }

                        // 保存到缓存
                        await _cacheService.SetAsync(options.PreferredWebsite, websiteId, media,
                            CacheSource.Manual);

                        return media;
                    }
                }

                // 如果ID获取失败或没有ID，尝试常规方式
                if (progressReporter != null)
                {
                    await progressReporter.ReportAsync($"尝试常规方式获取: {preferredWebsite.Name}");
                }
                var preferredSearchAttempt = IdentificationDiagnosticsContext.BeginAttempt(
                    preferredWebsite.Name, WebsiteAttemptSource.Search);
                var normalMedia = await SafeInvokeWebsiteAsync(
                    preferredWebsite.Name,
                    () => preferredWebsite.GetMediaInfoAsync(mediaSource, progressReporter, cancellationToken),
                    progressReporter,
                    cancellationToken,
                    preferredSearchAttempt);
                if (normalMedia != null)
                {
                    Log.Information("使用优先网站成功获取媒体信息：{MediaTitle}", normalMedia.Title);
                    if (progressReporter != null)
                    {
                        await progressReporter.SuccessAsync($"使用优先网站成功: {normalMedia.Title}");
                    }
                    return normalMedia;
                }
            }
            else
            {
                Log.Warning("未找到或未启用指定的优先网站: {Website}", options.PreferredWebsite);
                if (progressReporter != null)
                {
                    await progressReporter.WarningAsync($"未找到或未启用指定的优先网站: {options.PreferredWebsite}");
                }
                IdentificationDiagnosticsContext.RecordSkippedWebsite(
                    options.PreferredWebsite, "未找到或未启用指定的优先网站");
            }

            // 如果策略是手动模式且优先网站失败，直接返回null
            if (options.Strategy == IdentificationStrategy.Manual)
            {
                Log.Warning("手动模式下优先网站获取失败，不尝试其他网站");
                if (progressReporter != null)
                {
                    await progressReporter.WarningAsync("手动模式下优先网站获取失败，不尝试其他网站");
                }
                if (IdentificationDiagnosticsContext.Current is { } ctxManual)
                {
                    ctxManual.OverallFailureReason = "手动模式下优先网站获取失败";
                }
                return null;
            }
        }
        
        // 检查是否有网站优先级覆盖
        var websiteList = options?.WebsitePriorityOverride != null
            ? GetWebsitesByPriority(options.WebsitePriorityOverride)
            : GetWebsitesByCategory(mediaSource.PossibleTopCategory);

        var websiteIndex = 0;
        var enabledWebsites = websiteList.Where(w => w.Enable).ToList();

        foreach (var website in enabledWebsites)
        {
            // 检查是否已取消
            cancellationToken.ThrowIfCancellationRequested();

            // 若媒体源分类明确，且该网站自我声明不支持该分类 → 跳过（避免无效调用 + 下游类型错配）。
            // Unknown 表示无法从文件名推断类型，此时不跳任何网站，维持"尝试所有已启用网站"的兜底行为。
            if (mediaSource.PossibleTopCategory != TopCategory.Unknown
                && !website.TopCategories.Contains(mediaSource.PossibleTopCategory))
            {
                Log.Debug("跳过不支持分类 {Category} 的网站：{WebsiteName}",
                    mediaSource.PossibleTopCategory, website.Name);
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync(
                        $"跳过不支持 {mediaSource.PossibleTopCategory} 的网站: {website.Name}");
                }
                IdentificationDiagnosticsContext.RecordSkippedWebsite(
                    website.Name, $"不支持分类 {mediaSource.PossibleTopCategory}");
                continue;
            }

            websiteIndex++;
            Log.Debug("尝试使用网站：{WebsiteName}", website.Name);
            if (progressReporter != null)
            {
                await progressReporter.ReportAsync($"尝试网站 ({websiteIndex}/{enabledWebsites.Count}): {website.Name}");
            }

            // 检查是否有该网站的特定ID
            var specificId = options?.GetWebsiteId(website.Name);
            MediaBase? media = null;

            if (!string.IsNullOrEmpty(specificId))
            {
                Log.Debug("尝试使用网站特定ID: {Website} - {Id}", website.Name, specificId);
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"尝试使用网站特定ID: {specificId}");
                }
                var byIdAttempt = IdentificationDiagnosticsContext.BeginAttempt(
                    website.Name, WebsiteAttemptSource.ById, specificId);
                media = await SafeInvokeWebsiteAsync(
                    website.Name,
                    () => website.GetMediaInfoAsync(specificId, mediaSource, progressReporter, cancellationToken),
                    progressReporter,
                    cancellationToken,
                    byIdAttempt);
            }

            // 如果ID获取失败或没有ID，尝试常规方式
            if (media == null)
            {
                var searchAttempt = IdentificationDiagnosticsContext.BeginAttempt(
                    website.Name, WebsiteAttemptSource.Search);
                media = await SafeInvokeWebsiteAsync(
                    website.Name,
                    () => website.GetMediaInfoAsync(mediaSource, progressReporter, cancellationToken),
                    progressReporter,
                    cancellationToken,
                    searchAttempt);
            }

            if (media != null)
            {
                Log.Information("成功获取媒体信息：{MediaTitle} (来自 {Website})", media.Title, website.Name);
                if (progressReporter != null)
                {
                    await progressReporter.SuccessAsync($"识别成功: {media.Title} (来自 {website.Name})");
                }
                return media;
            }
            else
            {
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"网站 {website.Name} 未找到匹配结果");
                }
            }
        }

        Log.Warning("未找到任何网站能够获取媒体信息：{MediaSourceFullPath}", mediaSource.FullPath);
        if (progressReporter != null)
        {
            await progressReporter.WarningAsync("所有网站均未找到匹配结果");
        }
        if (IdentificationDiagnosticsContext.Current is { } ctxAllFail)
        {
            ctxAllFail.OverallFailureReason = "所有网站均未找到匹配结果";
        }
        return null;
    }

    public async Task<IEnumerable<MediaBase>> GetMediaInfoAsync(IEnumerable<MediaSource> mediaSources)
    {
        var tasks = mediaSources.Select(async mediaSource =>
        {
            var media = await GetMediaInfoAsync(mediaSource, null, null, CancellationToken.None);
            return new { mediaSource, media };
        });

        var results = await Task.WhenAll(tasks);

        return results
            .Where(result => result.media != null)
            .Select(result => result.media!);
    }
    
    /// <summary>
    /// 通过特定网站和ID识别媒体
    /// </summary>
    /// <param name="websiteName">网站名称</param>
    /// <param name="websiteId">网站特定ID</param>
    /// <param name="mediaSource">媒体源（可选）</param>
    /// <param name="options">识别选项（可选）</param>
    /// <param name="progressReporter">进度报告器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>媒体信息</returns>
    public async Task<MediaBase?> IdentifyBySpecificWebsiteAsync(
        string websiteName,
        string websiteId,
        MediaSource? mediaSource = null,
        IdentificationOptions? options = null,
        IProgressReporter? progressReporter = null,
        CancellationToken cancellationToken = default)
    {
        Log.Information("开始特定网站识别: 网站={Website}, ID={Id}", websiteName, websiteId);
        if (progressReporter != null)
        {
            await progressReporter.ReportAsync($"开始特定网站识别: {websiteName} - {websiteId}");
        }

        // 检查是否已取消
        cancellationToken.ThrowIfCancellationRequested();

        if (!WebsiteNameMap.TryGetValue(websiteName, out var website))
        {
            Log.Warning("未找到指定网站: {Website}", websiteName);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"未找到指定网站: {websiteName}");
            }
            return null;
        }

        if (!website.Enable)
        {
            Log.Warning("网站未启用: {Website}", websiteName);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"网站未启用: {websiteName}");
            }
            return null;
        }

        try
        {
            var media = await website.GetMediaInfoAsync(websiteId, mediaSource, progressReporter, cancellationToken);

            if (media != null)
            {
                Log.Information("特定网站识别成功: {Title}", media.Title);
                if (progressReporter != null)
                {
                    await progressReporter.SuccessAsync($"特定网站识别成功: {media.Title}");
                }

                // 如果有自定义名称，覆盖媒体标题
                if (!string.IsNullOrEmpty(options?.CustomIdentificationName))
                {
                    Log.Debug("使用自定义名称覆盖标题: {CustomName}", options.CustomIdentificationName);
                    media.Title = options.CustomIdentificationName;
                }
            }
            else
            {
                Log.Warning("特定网站识别失败: 网站={Website}, ID={Id}", websiteName, websiteId);
                if (progressReporter != null)
                {
                    await progressReporter.WarningAsync($"特定网站识别失败: {websiteName} - {websiteId}");
                }
            }

            return media;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "特定网站识别异常: 网站={Website}, ID={Id}", websiteName, websiteId);
            if (progressReporter != null)
            {
                await progressReporter.ErrorAsync($"特定网站识别异常: {ex.Message}");
            }

            return null;
        }
    }
    
    /// <summary>
    /// 获取指定网站实例
    /// </summary>
    /// <param name="websiteName">网站名称</param>
    /// <returns>网站实例</returns>
    public IWebsite? GetWebsiteByName(string websiteName)
    {
        return WebsiteNameMap.TryGetValue(websiteName, out var website) ? website : null;
    }
    
    /// <summary>
    /// 根据优先级列表获取网站
    /// </summary>
    /// <param name="priority">网站名称优先级列表</param>
    /// <returns>网站列表</returns>
    private IEnumerable<IWebsite> GetWebsitesByPriority(List<string> priority)
    {
        var websites = new List<IWebsite>();
        
        foreach (var websiteName in priority)
        {
            if (WebsiteNameMap.TryGetValue(websiteName, out var website))
            {
                websites.Add(website);
            }
        }
        
        return websites;
    }
    
    /// <summary>
    /// 根据分类获取网站列表
    /// </summary>
    /// <param name="category">媒体分类</param>
    /// <returns>网站列表</returns>
    private IEnumerable<IWebsite> GetWebsitesByCategory(TopCategory category)
    {
        var websites = new List<IWebsite>();

        // 优先添加该分类的网站
        if (Websites.TryGetValue(category, out var categoryWebsites))
        {
            websites.AddRange(categoryWebsites);
        }

        // 然后添加其他分类的网站
        foreach (var (topCategory, otherWebsites) in Websites)
        {
            if (topCategory == category) continue;

            foreach (var website in otherWebsites)
            {
                if (!websites.Contains(website))
                {
                    websites.Add(website);
                }
            }
        }

        return websites;
    }

    /// <summary>
    /// 统一包裹单个网站调用：把 HTTP 超时 / 解析异常等**偶发故障**转成 null，
    /// 保证 fallback 链路不会因为单个网站的网络抖动被击穿。
    /// 仅在外层 CancellationToken 被用户主动触发时才重新抛出，保证"取消任务"依然能中断。
    /// 当传入 <paramref name="attempt"/> 时，自动写入对应 attempt 的 Status/Duration/Reason。
    /// </summary>
    /// <param name="websiteName">网站名，仅用于日志 / 进度报告</param>
    /// <param name="call">对该网站的一次调用（ID 或源形式）</param>
    /// <param name="progressReporter">进度报告器（可选）</param>
    /// <param name="cancellationToken">外层取消令牌</param>
    /// <param name="attempt">当前识别任务的诊断 attempt（可选）</param>
    /// <returns>识别结果；失败时返回 null</returns>
    private static async Task<MediaBase?> SafeInvokeWebsiteAsync(
        string websiteName,
        Func<Task<MediaBase?>> call,
        IProgressReporter? progressReporter,
        CancellationToken cancellationToken,
        WebsiteAttemptDiagnostic? attempt = null)
    {
        try
        {
            var media = await call();
            IdentificationDiagnosticsContext.EndAttempt(attempt, media, null);
            return media;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户主动取消整个识别任务 → 必须让上层感知
            IdentificationDiagnosticsContext.EndAttempt(attempt, null, null, "用户取消");
            throw;
        }
        catch (Exception ex)
        {
            // 偶发失败（HTTP 超时、DOM 结构变动、JSON 解析错误等）→ 降级为"未匹配"，继续 fallback
            Log.Warning(ex, "网站 {Website} 识别异常，将作为未匹配处理", websiteName);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[{websiteName}] 识别异常，跳过: {ex.Message}");
            }
            IdentificationDiagnosticsContext.EndAttempt(attempt, null, ex);
            return null;
        }
    }
}