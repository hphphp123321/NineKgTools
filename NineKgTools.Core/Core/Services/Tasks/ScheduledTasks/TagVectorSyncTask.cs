using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Tasks.Base;
using NineKgTools.Core.Services.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.ScheduledTasks;

/// <summary>
/// 向量数据库同步任务
/// </summary>
[ScheduledTask("TagVectorSync", "标签向量同步", TaskType.TagVectorSync)]
public class TagVectorSyncTask : ScheduledTaskBase
{
    private readonly TagService _tagService;
    private readonly VectorService? _vectorDb;
    private readonly Config _config;
    
    public override TaskType TaskType => TaskType.TagVectorSync;
    public override string TaskName => "向量数据库同步任务";
    public override string? TaskDescription => "同步媒体数据库和向量数据库中的标签数据，确保向量数据的完整性和一致性";
    
    public TagVectorSyncTask(
        TagService tagService,
        Config config,
        VectorService? vectorDb = null)
    {
        _tagService = tagService;
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
                return TaskResult.CreateSuccess(TaskId, TaskName, "向量存储未启用，跳过同步任务");
            }

            Log.Information("开始执行向量数据库同步任务");

            var addedCount = 0;
            var updatedCount = 0;
            var deletedCount = 0;
            var failedCount = 0;

            // 1. 获取所有数据库标签
            var allTags = await _tagService.GetAllTagsAsync();

            if (cancellationToken.IsCancellationRequested)
                return TaskResult.CreateSuccess(TaskId, TaskName, "任务已取消");

            // 2. 处理新增和更新的标签
            var batchSize = GetBatchSize(parameters);
            var batches = allTags.Chunk(batchSize);

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                foreach (var tag in batch)
                {
                    try
                    {
                        var forceUpdate = ShouldUpdateVector(tag, parameters);
                        var vectorId = $"tag_{tag.Id}";
                        var existsBefore = await _vectorDb!.ExistsTagAsync(vectorId);

                        // 使用 TagService 同步向量
                        await _tagService.SyncTagVectorAsync(tag, forceUpdate);

                        // 统计
                        if (!existsBefore)
                        {
                            addedCount++;
                        }
                        else if (forceUpdate)
                        {
                            updatedCount++;
                        }

                        // 避免请求过快
                        if ((addedCount + updatedCount) % 10 == 0)
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "处理标签向量失败: {TagId} - {TagName}", tag.Id, tag.Name);
                        failedCount++;
                    }
                }
            }

            // 3. 清理已删除的标签向量（可选功能）
            if (GetCleanupOrphans(parameters))
            {
                // 这里需要实现获取所有向量ID并与数据库比对的逻辑
                // 由于当前API限制，暂时跳过此功能
                Log.Debug("跳过孤立向量清理（功能待实现）");
            }

            var message = $"向量同步完成: 新增 {addedCount} 个, 更新 {updatedCount} 个, 删除 {deletedCount} 个, 失败 {failedCount} 个";
            Log.Information(message);

            return TaskResult.CreateSuccess(TaskId, TaskName, message, addedCount + updatedCount + deletedCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "向量数据库同步任务执行失败");
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

        return true;
    }
    
    /// <summary>
    /// 判断是否应该使用向量存储
    /// </summary>
    private bool ShouldUseVectorStorage()
    {
        return _tagService.ShouldUseVectorStorage() && _vectorDb != null;
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
    private bool ShouldUpdateVector(Models.Tags.Tag tag, Dictionary<string, object>? parameters)
    {
        // 如果强制更新参数为true，则更新
        if (parameters?.TryGetValue("force_update", out var value) == true && value is bool forceUpdate)
        {
            return forceUpdate;
        }
        
        // 默认不更新已存在的向量（可以根据实际需求实现更复杂的逻辑）
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
    
}