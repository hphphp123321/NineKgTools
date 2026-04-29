using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Source;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Core.Services.Tasks.IdentificationTasks;
using NineKgTools.Core.Services.Tasks.MonitorTasks;
using NineKgTools.Core.Services.Websites;
using Hangfire;
using Serilog;

namespace NineKgTools.Core.Services.Files;

/// <summary>
/// 负责管理文件源、加入数据库
/// </summary>
public class FilesService
{
    private readonly Config _config;
    private readonly MonitorService _monitorService;
    private readonly SourceService _sourceService;
    private readonly WebsiteService _websiteService;
    private readonly MediaService _mediaService;
    private readonly PendingIdentificationService _pendingIdentificationService;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly UnifiedTaskService _taskService;

    public FilesService(Config config, SourceService sourceService,
        WebsiteService websiteService, MediaService mediaService,
        PendingIdentificationService pendingIdentificationService,
        ImageService imageService, UnifiedTaskService taskService,
        TaskProgressService taskProgressService, IServiceScopeFactory serviceScopeFactory,
        MonitorService monitorService)
    {
        _taskService = taskService;
        _sourceService = sourceService;
        _websiteService = websiteService;
        _mediaService = mediaService;
        _pendingIdentificationService = pendingIdentificationService;
        _config = config;
        _serviceScopeFactory = serviceScopeFactory;
        _monitorService = monitorService;
    }

    /// <summary>
    /// 【对外接口】批量识别文件夹中的媒体文件
    /// </summary>
    /// <param name="directoryPath">文件夹路径</param>
    /// <param name="options">识别选项，为空即默认</param>
    /// <param name="maxDepth">识别深度，0即directoryPath下的一层所有子文件夹</param>
    /// <param name="startMonitoringAfterCompletion">完成后是否启动监控</param>
    /// <returns>任务ID，可用于查询进度或取消任务</returns>
    public async Task<string> IdentifyBatchMedia(string directoryPath, IdentificationOptions? options = null, int maxDepth = 0, bool startMonitoringAfterCompletion = true)
    {
        Log.Information("开始解析文件夹: {DirectoryPath}", directoryPath);

        // 检查重复监控
        if (startMonitoringAfterCompletion && _monitorService.IsMonitoring(directoryPath))
        {
            Log.Warning("文件夹已在监控中，跳过启动新监控: {Path}", directoryPath);
            startMonitoringAfterCompletion = false;
        }

        // 创建文件夹批量识别任务
        var task = new BatchSourceIdentificationTask(
            _serviceScopeFactory, // 传递ServiceScopeFactory而不是this
            directoryPath,
            options ?? _config.Identification.ToIdentificationOptions(), // 使用配置的默认识别选项
            maxDepth: 0, // 只处理一级
            extensions: null, // 处理所有支持的扩展名
            TaskPriority.Normal,
            startMonitoringAfterCompletion,
            options  // monitorOptions
        );

        // 提交任务到队列
        var taskId = await _taskService.SubmitParentTaskAsync(task);

        Log.Information("文件夹处理任务已提交: {Path}, TaskId: {TaskId}. {MonitorMessage}",
            directoryPath, taskId,
            startMonitoringAfterCompletion ? "任务完成后将自动开始监控文件夹" : "");

        return taskId;
    }

    /// <summary>
    /// 【对外接口】识别单个媒体文件或文件夹
    /// </summary>
    /// <param name="path">文件或文件夹路径</param>
    /// <param name="options">识别选项</param>
    /// <returns>任务ID，可用于查询进度或取消任务</returns>
    public async Task<string> IdentifySingleMedia(string path, IdentificationOptions? options = null)
    {
        if (!IsValidMediaSource(path))
        {
            throw new ArgumentException($"路径 '{path}' 不是有效的媒体源", nameof(path));
        }

        Log.Information("提交单个媒体识别任务: {Path}", path);

        // 创建单个识别任务
        var task = new SingleSourceIdentificationTask(
            _serviceScopeFactory,
            path,
            options ?? _config.Identification.ToIdentificationOptions(),
            TaskPriority.High  // 用户手动触发的任务优先级更高
        );

        // 提交到任务队列并返回 TaskId
        var taskId = await _taskService.SubmitTaskAsync(task);

        Log.Information("单个媒体识别任务已提交: {Path}, TaskId: {TaskId}", path, taskId);

        return taskId;
    }

    /// <summary>
    /// 【对内接口 - 仅供 Task 层使用】
    /// 通过路径识别媒体信息，执行实际的网站查询和识别操作
    /// </summary>
    /// <param name="path"> 文件路径 </param>
    /// <param name="options"> 识别选项配置 </param>
    /// <param name="progressReporter"> 进度报告器（可选） </param>
    /// <param name="cancellationToken"> 取消令牌 </param>
    /// <returns> 媒体信息 </returns>
    /// <remarks>
    /// 此方法是核心识别逻辑，由 SingleSourceIdentificationTask 和 BatchSourceIdentificationTask 调用。
    /// 前端应使用 IdentifySingleMedia 或 IdentifyBatchMedia 提交任务到队列。
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]  // 在 IDE 中隐藏此方法
    public async Task<MediaBase?> GetMediaByPath(string path, IdentificationOptions? options, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        // 通知识别开始
        if (progressReporter != null)
        {
            await progressReporter.StartAsync($"识别媒体: {Path.GetFileName(path)}", 1);
        }

        try
        {
        // 检查是否已取消
        cancellationToken.ThrowIfCancellationRequested();

        // 如果提供了识别选项并且是手动模式，使用手动识别流程
        if (options != null && options.Strategy == IdentificationStrategy.Manual)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("使用手动识别模式", path);
            }
            return await HandleManualIdentificationWithProgress(path, options, progressReporter, cancellationToken);
        }

        if (!IsValidMediaSource(path))
        {
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync("文件名不合法，跳过处理", null, path);
            }
            Log.Debug("文件名不合法，跳过处理: {FileName}", path);
            return null;
        }

        var mediaSource = MediaSourceFactory.Create(path);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"解析媒体源: {mediaSource.GetFileName()}", path);
        }

        // 如果提供了识别选项，将选项信息附加到MediaSource
        if (options != null)
        {
            AttachOptionsToMediaSource(mediaSource, options);
        }

        var mediaDbSource = await _sourceService.FindMediaSourceAsync(mediaSource);
        if (mediaDbSource != null)
        {
            // 根据识别策略决定是否使用缓存
            if (options?.SkipCache == true || options?.Strategy == IdentificationStrategy.ForceRefresh)
            {
                if (progressReporter != null)
                {
                    await progressReporter.InfoAsync("根据识别选项跳过缓存，重新识别", 20, path);
                }
                Log.Information("根据识别选项跳过缓存，重新识别媒体: {Path}", path);
            }
            else if (mediaDbSource is { InDatabase: true, MediaBase: not null })
            {
                var dbMedia = await _mediaService.GetMediaAsync(mediaDbSource.MediaBase.Id);
                if (progressReporter != null)
                {
                    await progressReporter.SuccessAsync($"使用缓存: {dbMedia.Title}", 100, path);
                }
                Log.Debug("媒体源：{MediaSourceFullPath} 已经存在并且已处理，对应媒体: {MediaTtile}", mediaSource.FullPath,
                    dbMedia.Title);
                return dbMedia;
            }

            mediaSource = mediaDbSource; // 用数据库中的数据覆盖，重新解析
        }

        // 更新进度：正在查询网站
        if (progressReporter != null)
        {
            await progressReporter.InfoAsync("正在查询网站数据...", 50, path);
        }

        // 传递识别选项、进度报告器和CancellationToken到WebsiteService
        var media = await _websiteService.GetMediaInfoAsync(mediaSource, options, progressReporter, cancellationToken);

        // 通知完成或失败
        if (progressReporter != null)
        {
            if (media != null)
            {
                await progressReporter.CompleteAsync($"识别成功: {media.Title}", 1, 0);
            }
            else
            {
                await progressReporter.FailAsync("未能识别媒体信息");
            }
        }

        // 识别失败时，将媒体源保存到数据库（标记为未识别、未入库），以便在待处理页面中显示
        if (media == null && mediaDbSource == null)
        {
            mediaSource.Identified = false;
            mediaSource.InDatabase = false;
            await _sourceService.AddMediaSourceAsync(mediaSource);
        }

        return media;
        }
        catch (Exception ex)
        {
            // 通知失败
            if (progressReporter != null)
            {
                await progressReporter.FailAsync($"识别失败: {ex.Message}", ex);
            }

            Log.Error(ex, "识别媒体时发生错误: {Path}", path);
            throw;
        }
    }

    /// <summary>
    /// 将媒体信息添加到数据库中（正式入库）。
    /// 会把对应 MediaSource 的 Identified 和 InDatabase 标记为 true，
    /// 并清理可能存在的 PendingIdentification 行。
    /// </summary>
    /// <param name="media"> 媒体信息 </param>
    /// <param name="cancellationToken"> 取消令牌 </param>
    public async Task AddMediaToDatabase(MediaBase media, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (media.Source == null)
            throw new InvalidOperationException("AddMediaToDatabase: media.Source 不能为 null");

        // 防御性修复：IdentificationCacheService 是单例，会把 MediaBase 连同 Source 引用一起跨作用域
        // 缓存。如果本次任务已经在当前 DbContext 里通过 FindMediaSourceAsync 跟踪了一个同 Id 的
        // MediaSource 实例，下面的 AddAsync 会触发 EF 的"another instance ... already being tracked"
        // 冲突。这里把 media.Source 重绑到当前作用域跟踪的那一份，确保只有一个 C# 实例。
        var dbSource = await _sourceService.FindMediaSourceAsync(media.Source);
        if (dbSource != null)
        {
            // 把缓存里带过来的最新字段同步到跟踪实例上（路径/类型等通常不变，但保持幂等）
            dbSource.IsFolder = media.Source.IsFolder;
            dbSource.PossibleTopCategory = media.Source.PossibleTopCategory;
            dbSource.EntryFilePath = media.Source.EntryFilePath;
            dbSource.Identified = true;
            dbSource.InDatabase = true;
            media.Source = dbSource;
        }
        else
        {
            // 当前 DbContext 里没有对应记录 → 作为新行插入。
            // 重置 Id 为 0，避免缓存里带过来的陈旧 Id 让 EF 误以为是更新现有行。
            media.Source.Id = 0;
            media.Source.Identified = true;
            media.Source.InDatabase = true;
        }

        await _mediaService.AddOrUpdateMediaAsync(media);

        // 清理对应的 pending 暂存（若存在）。
        // 用 SourceId > 0 判断是否有对应 MediaSource 数据库记录。
        if (media.Source.Id > 0)
        {
            await _pendingIdentificationService.RemoveBySourceIdAsync(media.Source.Id, cancellationToken);
        }
    }

    /// <summary>
    /// 将"已识别但不立即入库"的 MediaBase 结果暂存到 PendingIdentification 表，
    /// 对应 MediaSource 设为 Identified = true, InDatabase = false。
    /// 由后台识别任务在 AutoAddToDatabase = false 时调用。
    /// </summary>
    public async Task SaveIdentifiedButPendingAsync(MediaBase media, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (media.Source == null)
            throw new InvalidOperationException("SaveIdentifiedButPendingAsync: media.Source 不能为 null");

        // 找到或写入对应 MediaSource 数据库记录。
        // 与 AddMediaToDatabase 一致：若已存在跟踪实例就复用，避免陈旧引用冲突。
        var dbSource = await _sourceService.FindMediaSourceAsync(media.Source);
        if (dbSource == null)
        {
            // 重置 Id 避免缓存里的陈旧 Id 影响插入
            media.Source.Id = 0;
            media.Source.Identified = true;
            media.Source.InDatabase = false;
            dbSource = await _sourceService.AddMediaSourceAsync(media.Source);
        }
        else
        {
            dbSource.IsFolder = media.Source.IsFolder;
            dbSource.PossibleTopCategory = media.Source.PossibleTopCategory;
            dbSource.EntryFilePath = media.Source.EntryFilePath;
            dbSource.Identified = true;
            dbSource.InDatabase = false;
        }

        // 重绑 media.Source 到跟踪实例，与 AddMediaToDatabase 保持一致
        media.Source = dbSource;

        await _pendingIdentificationService.SaveAsync(dbSource, media, cancellationToken);
        Log.Information("媒体已暂存为待入库: {Title} ({FilePath})", media.Title, dbSource.FullPath);
    }
    
    /// <summary>
    /// 通过路径删除媒体
    /// </summary>
    /// <param name="path"> 文件路径 </param>
    /// <param name="cancellationToken"> 取消令牌 </param>
    /// <returns></returns>
    public async Task RemoveMediaByPath(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tempSource = MediaSourceFactory.Create(path);
        var realSource = await _sourceService.FindMediaSourceAsync(tempSource);

        if (realSource == null) return; // 数据库中无此路径记录

        if (realSource.MediaBase != null)
        {
            // 有关联 Media：删除 Media（数据库级联删除 MediaSource）
            await _mediaService.RemoveMediaAsync(realSource.MediaBase.Id);
            Log.Information("文件删除：已移除媒体及其源记录 {Path}", path);
        }
        else
        {
            // 孤立 MediaSource（源存在但无 Media）：直接删除源记录
            await _sourceService.RemoveMediaSourceAsync(realSource);
            Log.Information("文件删除：已移除孤立媒体源记录 {Path}", path);
        }
    }


    #region 私有方法
    
    /// <summary>
    /// 处理手动识别流程（带进度通知）
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="options">识别选项</param>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>媒体信息</returns>
    private async Task<MediaBase?> HandleManualIdentificationWithProgress(string path, IdentificationOptions options, IProgressReporter? progressReporter, CancellationToken cancellationToken = default)
    {
        // 检查是否已取消
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(options.PreferredWebsite))
        {
            Log.Warning("手动识别模式需要指定网站名称");
            return null;
        }
        
        var websiteId = options.GetWebsiteId(options.PreferredWebsite);
        if (string.IsNullOrEmpty(websiteId))
        {
            Log.Warning("手动识别模式需要指定网站ID");
            return null;
        }
        
        // 验证ID格式
        if (!WebsiteIdValidatorManager.ValidateId(options.PreferredWebsite, websiteId))
        {
            Log.Warning("网站ID格式无效: {Website} - {Id}", options.PreferredWebsite, websiteId);
            return null;
        }
        
        Log.Information("使用手动识别模式: 网站={Website}, ID={Id}, 名称={Name}", 
            options.PreferredWebsite, websiteId, options.CustomIdentificationName);
        
        // 创建MediaSource（如果提供了路径）
        MediaSource? mediaSource = null;
        if (!string.IsNullOrEmpty(path))
        {
            mediaSource = MediaSourceFactory.Create(path);
            AttachOptionsToMediaSource(mediaSource, options);
        }
        
        // 调用WebsiteService进行手动识别
        var media = await _websiteService.IdentifyBySpecificWebsiteAsync(
            options.PreferredWebsite,
            websiteId,
            mediaSource,
            options,
            progressReporter,
            cancellationToken);
            
        if (media != null)
        {
            Log.Information("手动识别成功: {Title}", media.Title);
            
            // 通知成功
            if (progressReporter != null)
            {
                await progressReporter.CompleteAsync($"识别成功: {media.Title}", 1, 0);
            }
        }
        else
        {
            Log.Warning("手动识别失败");
            
            // 通知失败
            if (progressReporter != null)
            {
                await progressReporter.FailAsync($"手动识别失败: {options.PreferredWebsite} - {websiteId}");
            }
        }
        
        return media;
    }
    
    /// <summary>
    /// 将识别选项附加到MediaSource
    /// </summary>
    /// <param name="mediaSource">媒体源</param>
    /// <param name="options">识别选项</param>
    private void AttachOptionsToMediaSource(MediaSource mediaSource, IdentificationOptions options)
    {
        // 由于MediaSource没有Infos属性，我们通过其他方式传递选项信息
        // 选项信息将通过IdentificationOptions参数直接传递给WebsiteService
        // 这里可以记录日志或进行其他处理
        
        if (!string.IsNullOrEmpty(options.CustomIdentificationName))
        {
            Log.Debug("将使用自定义识别名称: {CustomName}", options.CustomIdentificationName);
        }
        
        if (!string.IsNullOrEmpty(options.PreferredWebsite))
        {
            Log.Debug("将优先使用网站: {Website}", options.PreferredWebsite);
        }
        
        if (!string.IsNullOrEmpty(options.WebsiteSpecificId))
        {
            Log.Debug("将使用网站特定ID: {Id}", options.WebsiteSpecificId);
        }
    }

    /// <summary>
    /// 验证文件是否为有效的媒体文件（根据配置过滤）
    /// </summary>
    /// <param name="pathOrFileName">文件路径或文件名</param>
    /// <returns>如果文件有效返回true，否则返回false</returns>
    public bool IsValidMediaSource(string pathOrFileName)
    {
        // 获取文件名
        string fileName = Path.GetFileName(pathOrFileName);
        
        // 获取配置中的忽略列表
        var ignoredFiles = _config.Files?.IgnoredFiles ?? new List<string>();
        var ignoredPatterns = _config.Files?.IgnoredPatterns ?? new List<string>();
        
        // 精确匹配（不区分大小写）
        foreach (var ignoredFile in ignoredFiles)
        {
            if (fileName.Equals(ignoredFile, StringComparison.OrdinalIgnoreCase))
            {
                Log.Debug("文件名 {FileName} 匹配忽略列表中的 {IgnoredFile}", fileName, ignoredFile);
                return false;
            }
        }
        
        // 模式匹配
        foreach (var pattern in ignoredPatterns)
        {
            if (MatchesPattern(fileName, pattern))
            {
                Log.Debug("文件名 {FileName} 匹配忽略模式 {Pattern}", fileName, pattern);
                return false;
            }
        }
        
        // 如果是完整路径，检查文件属性和大小
        if (File.Exists(pathOrFileName))
        {
            var fileInfo = new FileInfo(pathOrFileName);
            var fileConfig = _config.Files;

            // 检查隐藏文件
            if (fileConfig?.SkipHiddenFiles == true &&
                (fileInfo.Attributes & FileAttributes.Hidden) != 0)
            {
                Log.Debug("文件 {FilePath} 是隐藏文件，跳过处理", pathOrFileName);
                return false;
            }

            // 检查系统文件
            if (fileConfig?.SkipSystemFiles == true &&
                (fileInfo.Attributes & FileAttributes.System) != 0)
            {
                Log.Debug("文件 {FilePath} 是系统文件，跳过处理", pathOrFileName);
                return false;
            }

            // 检查文件大小
            var minimumSize = fileConfig?.MinimumFileSize ?? 1024; // 默认1KB
            if (fileInfo.Length < minimumSize)
            {
                Log.Debug("文件 {FilePath} 大小 {Size} 字节，小于最小文件大小 {MinSize} 字节，跳过处理",
                    pathOrFileName, fileInfo.Length, minimumSize);
                return false;
            }
        }
        else if (Directory.Exists(pathOrFileName))
        {
            var dirInfo = new DirectoryInfo(pathOrFileName);
            var fileConfig = _config.Files;

            // 检查隐藏目录
            if (fileConfig?.SkipHiddenFiles == true &&
                (dirInfo.Attributes & FileAttributes.Hidden) != 0)
            {
                Log.Debug("目录 {Path} 是隐藏目录，跳过处理", pathOrFileName);
                return false;
            }

            // 检查系统目录
            if (fileConfig?.SkipSystemFiles == true &&
                (dirInfo.Attributes & FileAttributes.System) != 0)
            {
                Log.Debug("目录 {Path} 是系统目录，跳过处理", pathOrFileName);
                return false;
            }

            // 检查目录是否为空
            if (!dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Any())
            {
                Log.Debug("目录 {Path} 为空，跳过处理", pathOrFileName);
                return false;
            }
        }

        // 检查扩展名（如果配置了允许的扩展名列表，仅对文件检查）
        var allowedExtensions = _config.Files?.AllowedExtensions;
        if (allowedExtensions != null && allowedExtensions.Any() && !Directory.Exists(pathOrFileName))
        {
            var extension = Path.GetExtension(pathOrFileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                Log.Debug("文件 {FilePath} 的扩展名 {Extension} 不在允许列表中，跳过处理",
                    pathOrFileName, extension);
                return false;
            }
        }

        return true;
    }
    
    /// <summary>
    /// 简单的通配符匹配
    /// </summary>
    /// <param name="fileName">文件名</param>
    /// <param name="pattern">模式（支持 * 通配符）</param>
    /// <returns>是否匹配</returns>
    private bool MatchesPattern(string fileName, string pattern)
    {
        // 转换为不区分大小写的比较
        fileName = fileName.ToLowerInvariant();
        pattern = pattern.ToLowerInvariant();
        
        // 处理特殊情况
        if (pattern == "*") return true;
        
        // 分割模式为前缀和后缀
        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            // *xxx* - 包含
            var middle = pattern.Substring(1, pattern.Length - 2);
            return fileName.Contains(middle);
        }
        else if (pattern.StartsWith("*"))
        {
            // *xxx - 以xxx结尾
            var suffix = pattern.Substring(1);
            return fileName.EndsWith(suffix);
        }
        else if (pattern.EndsWith("*"))
        {
            // xxx* - 以xxx开头
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return fileName.StartsWith(prefix);
        }
        else if (pattern.Contains("*"))
        {
            // xxx*yyy - 以xxx开头且以yyy结尾
            var parts = pattern.Split('*');
            if (parts.Length == 2)
            {
                return fileName.StartsWith(parts[0]) && fileName.EndsWith(parts[1]);
            }
        }
        
        // 没有通配符，精确匹配
        return fileName == pattern;
    }

    // 当文件或文件夹创建时触发的事件
    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath;

        // 使用统一的文件筛选逻辑
        if (!IsValidMediaSource(path))
        {
            Log.Debug("文件不合法，跳过创建任务: {Path}", path);
            return;
        }

        Log.Information("检测到新媒体源: {FullPath}", path);

        // 创建文件识别任务
        var task = new SingleSourceIdentificationTask(
            _serviceScopeFactory, // 使用ServiceScopeFactory
            path,
            _config.Identification.ToIdentificationOptions(), // 使用配置的默认识别选项
            TaskPriority.Normal
        );

        // 提交任务到队列
        await _taskService.SubmitTaskAsync(task);
    }

    // 当文件或文件夹删除时触发的事件
    private async void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        Log.Information("检测到删除文件夹: {FullPath}", e.FullPath);

        // 直接异步处理删除操作
        await Task.Run(() => RemoveMediaByPath(e.FullPath, CancellationToken.None));
    }

    #endregion

    /// <summary>
    /// 开始处理配置中的监视文件夹：先独立启动文件夹监控（保证实时响应文件变化），
    /// 再异步提交批量识别扫描已有文件。
    /// </summary>
    public async Task StartProcessConfiguredFolders()
    {
        foreach (var folder in _config.Source.WatchFolders)
        {
            if (string.IsNullOrWhiteSpace(folder))
                continue;

            if (!Directory.Exists(folder))
            {
                Log.Warning("监视文件夹不存在，跳过: {Folder}", folder);
                continue;
            }

            // Step 1: 独立启动监控，不依赖批量识别结果
            if (!_monitorService.IsMonitoring(folder))
            {
                try
                {
                    var monitorTask = new FolderMonitorTask(
                        _serviceScopeFactory,
                        folder,
                        _config.Identification.ToIdentificationOptions(),
                        TaskPriority.Low
                    );
                    await _taskService.SubmitTaskAsync(monitorTask);
                    Log.Information("已提交文件夹监控任务: {Folder}", folder);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "提交文件夹监控任务失败: {Folder}", folder);
                }
            }
            else
            {
                Log.Debug("文件夹已在监控中，跳过重复启动: {Folder}", folder);
            }

            // Step 2: 异步扫描已有文件（监控只负责新增/删除事件）；不再由批量识别负责起监控。
            try
            {
                await IdentifyBatchMedia(folder, startMonitoringAfterCompletion: false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "提交启动时批量识别失败: {Folder}", folder);
            }
        }
    }
}