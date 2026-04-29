using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Tasks.Base;
using NineKgTools.Core.Services.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.ScheduledTasks;

/// <summary>
/// 媒体向量数据库同步任务
/// </summary>
[ScheduledTask("MediaVectorSync", "媒体向量同步", TaskType.MediaVectorSync)]
public class MediaVectorSyncTask : ScheduledTaskBase
{
    private readonly MediaService _mediaService;
    private readonly VectorService? _vectorDb;
    private readonly Config _config;
    
    public override TaskType TaskType => TaskType.MediaVectorSync;
    public override string TaskName => "媒体向量数据库同步任务";
    public override string? TaskDescription => "同步媒体数据到向量数据库，确保媒体向量数据的完整性和一致性，支持语义搜索";
    
    public MediaVectorSyncTask(
        MediaService mediaService,
        Config config,
        VectorService? vectorDb = null)
    {
        _mediaService = mediaService;
        _config = config;
        _vectorDb = vectorDb;
    }
    
    public override async Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // 检查向量存储是否启用
            if (!ShouldUseVectorStorage())
            {
                return TaskResult.CreateSuccess(TaskId, TaskName, "向量存储未启用，跳过媒体同步任务");
            }

            Log.Information("开始执行媒体向量数据库同步任务");

            var addedCount = 0;
            var updatedCount = 0;
            var deletedCount = 0;
            var failedCount = 0;

            // 1. 获取所有媒体数据
            var queryParams = new MediaQueryParameters
            {
                PageSize = int.MaxValue,
                PageNumber = 1
            };
            var pagedResult = _mediaService.GetPagedMediaList(queryParams);
            var allMedia = pagedResult.ToList();

            if (cancellationToken.IsCancellationRequested)
                return TaskResult.CreateSuccess(TaskId, TaskName, "任务已取消");

            // 2. 处理新增和更新的媒体
            var batchSize = GetBatchSize(parameters);
            var batches = allMedia.Chunk(batchSize);

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (var media in batch)
                {
                    try
                    {
                        var vectorId = $"media_{media.Id}";

                        // 检查向量是否存在
                        if (await _vectorDb!.ExistsMediaAsync(vectorId))
                        {
                            // 检查是否需要更新
                            if (ShouldUpdateVector(media, parameters))
                            {
                                await UpdateMediaVectorAsync(media);
                                updatedCount++;
                            }
                        }
                        else
                        {
                            // 新增向量
                            await StoreMediaVectorAsync(media);
                            addedCount++;
                        }

                        // 避免请求过快
                        if ((addedCount + updatedCount) % 10 == 0)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "处理媒体向量失败: {MediaId} - {MediaTitle}", media.Id, media.Title);
                        failedCount++;
                    }
                }
            }

            // 3. 清理已删除的媒体向量（可选功能）
            if (GetCleanupOrphans(parameters))
            {
                // 这里需要实现获取所有向量ID并与数据库比对的逻辑
                // 由于当前API限制，暂时跳过此功能
                Log.Debug("跳过孤立媒体向量清理（功能待实现）");
            }

            var message = $"媒体向量同步完成: 新增 {addedCount} 个, 更新 {updatedCount} 个, 删除 {deletedCount} 个, 失败 {failedCount} 个";
            Log.Information(message);

            return TaskResult.CreateSuccess(TaskId, TaskName, message, addedCount + updatedCount + deletedCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "媒体向量数据库同步任务执行失败");
            return TaskResult.CreateFailure(TaskId, TaskName, $"任务执行失败: {ex.Message}", ex);
        }
    }
    
    public override bool ValidateParameters(Dictionary<string, object>? parameters)
    {
        if (parameters == null || !parameters.Any())
        {
            return true;
        }

        // 验证批处理大小 - 使用 try-catch 而非严格类型检查
        if (parameters.TryGetValue("batch_size", out var batchSize))
        {
            try
            {
                var size = Convert.ToInt32(batchSize);
                if (size < 1 || size > 1000)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        // 验证布尔参数 - 支持 bool 或可解析为 bool 的字符串
        if (parameters.TryGetValue("force_update", out var forceUpdate))
        {
            if (forceUpdate is not bool && !bool.TryParse(forceUpdate?.ToString(), out _))
                return false;
        }

        if (parameters.TryGetValue("cleanup_orphans", out var cleanup))
        {
            if (cleanup is not bool && !bool.TryParse(cleanup?.ToString(), out _))
                return false;
        }

        // 验证媒体类型过滤
        if (parameters.TryGetValue("media_type", out var mediaType))
        {
            var typeStr = mediaType?.ToString();
            if (string.IsNullOrEmpty(typeStr))
                return false;

            var validTypes = new[] { "Audio", "Video", "Game", "Picture", "Text", "All" };
            if (!validTypes.Contains(typeStr, StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
    
    /// <summary>
    /// 判断是否应该使用向量存储
    /// </summary>
    private bool ShouldUseVectorStorage()
    {
        return _config.Ai?.UseAi == true &&
               _config.Ai?.Vector?.Enable == true &&
               _config.Ai?.Vector?.Media?.Enable == true &&
               _vectorDb != null;
    }
    
    /// <summary>
    /// 获取批处理大小
    /// </summary>
    private int GetBatchSize(Dictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue("batch_size", out var value) == true)
        {
            return Convert.ToInt32(value);
        }
        
        return _config.Ai?.Vector?.Db?.BatchSize ?? 100;
    }
    
    /// <summary>
    /// 判断是否需要更新向量
    /// </summary>
    private bool ShouldUpdateVector(MediaBase media, Dictionary<string, object>? parameters)
    {
        // 如果强制更新参数为true，则更新
        if (parameters?.TryGetValue("force_update", out var value) == true && value is bool forceUpdate)
        {
            return forceUpdate;
        }
        
        // 默认不更新已存在的向量（可以根据实际需求实现更复杂的逻辑，比如检查更新时间）
        return false;
    }
    
    /// <summary>
    /// 是否清理孤立向量
    /// </summary>
    private bool GetCleanupOrphans(Dictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue("cleanup_orphans", out var value) == true && value is bool cleanup)
        {
            return cleanup;
        }
        
        return false;
    }
    
    /// <summary>
    /// 为媒体生成并存储向量
    /// </summary>
    private async Task StoreMediaVectorAsync(MediaBase media)
    {
        var mediaVector = await _mediaService.CreateMediaVectorAsync(media);
        await _vectorDb!.AddMediaVectorAsync(mediaVector);
        Log.Debug("成功存储媒体向量: {Id} - {Title}", media.Id, media.Title);
    }
    
    /// <summary>
    /// 更新媒体向量
    /// </summary>
    private async Task UpdateMediaVectorAsync(MediaBase media)
    {
        var mediaVector = await _mediaService.CreateMediaVectorAsync(media);
        await _vectorDb!.UpdateMediaVectorAsync(mediaVector);
        Log.Debug("成功更新媒体向量: {Id} - {Title}", media.Id, media.Title);
    }
    
}