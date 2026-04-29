using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks;

/// <summary>
/// 任务元数据存储服务 - 管理 ITask 实例和 Hangfire Job ID 的映射
/// </summary>
public class TaskMetadataStore : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, string> _jobToTaskMap;
    private readonly ConcurrentDictionary<string, string> _taskToJobMap;
    private readonly ConcurrentDictionary<string, int> _parentTaskCounters;
    private readonly ConcurrentDictionary<string, List<string>> _parentToChildJobs;
    private readonly ConcurrentDictionary<string, int> _retryCountMap;
    private readonly TimeSpan _cacheExpiration;
    
    public TaskMetadataStore(IMemoryCache cache)
    {
        _cache = cache;
        _jobToTaskMap = new ConcurrentDictionary<string, string>();
        _taskToJobMap = new ConcurrentDictionary<string, string>();
        _parentTaskCounters = new ConcurrentDictionary<string, int>();
        _parentToChildJobs = new ConcurrentDictionary<string, List<string>>();
        _retryCountMap = new ConcurrentDictionary<string, int>();
        _cacheExpiration = TimeSpan.FromHours(24); // 任务元数据保留24小时
    }
    
    /// <summary>
    /// 存储任务实例
    /// </summary>
    public async Task StoreTaskAsync(ITask task)
    {
        var cacheKey = GetTaskCacheKey(task.TaskId);
        
        // 存储到内存缓存
        _cache.Set(cacheKey, task, new MemoryCacheEntryOptions
        {
            SlidingExpiration = _cacheExpiration,
            Priority = CacheItemPriority.Normal,
            Size = 1, // 设置缓存条目大小
            PostEvictionCallbacks =
            {
                new PostEvictionCallbackRegistration
                {
                    EvictionCallback = (key, value, reason, state) =>
                    {
                        if (reason == EvictionReason.Expired)
                        {
                            Log.Debug("任务元数据已过期: {TaskId}", task.TaskId);
                        }
                    }
                }
            }
        });
        
        Log.Debug("已存储任务元数据: {TaskId} - {TaskName}", task.TaskId, task.TaskName);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 获取任务实例
    /// </summary>
    public async Task<ITask?> GetTaskAsync(string taskId)
    {
        var cacheKey = GetTaskCacheKey(taskId);
        
        if (_cache.TryGetValue<ITask>(cacheKey, out var task))
        {
            Log.Debug("从缓存获取任务元数据: {TaskId}", taskId);
            return task;
        }
        
        Log.Warning("未找到任务元数据: {TaskId}", taskId);
        return await Task.FromResult<ITask?>(null);
    }
    
    /// <summary>
    /// 移除任务实例
    /// </summary>
    public async Task RemoveTaskAsync(string taskId)
    {
        var cacheKey = GetTaskCacheKey(taskId);
        _cache.Remove(cacheKey);

        // 清理映射关系
        if (_taskToJobMap.TryRemove(taskId, out var jobId))
        {
            _jobToTaskMap.TryRemove(jobId, out _);
        }

        // 清理父任务的子任务Job ID列表
        _parentToChildJobs.TryRemove(taskId, out _);

        // 清理重试计数
        _retryCountMap.TryRemove(taskId, out _);

        Log.Debug("已移除任务元数据: {TaskId}", taskId);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 映射 Hangfire Job ID 到 Task ID
    /// </summary>
    public void MapJobToTask(string jobId, string taskId)
    {
        _jobToTaskMap[jobId] = taskId;
        _taskToJobMap[taskId] = jobId;
        
        Log.Debug("已建立任务映射: JobId={JobId} -> TaskId={TaskId}", jobId, taskId);
    }
    
    /// <summary>
    /// 根据 Job ID 获取 Task ID
    /// </summary>
    public string? GetTaskIdByJobId(string jobId)
    {
        return _jobToTaskMap.TryGetValue(jobId, out var taskId) ? taskId : null;
    }
    
    /// <summary>
    /// 根据 Task ID 获取 Job ID
    /// </summary>
    public string? GetJobIdByTaskId(string taskId)
    {
        return _taskToJobMap.TryGetValue(taskId, out var jobId) ? jobId : null;
    }
    
    /// <summary>
    /// 解除 Job ID 映射
    /// </summary>
    public void UnmapJob(string jobId)
    {
        if (_jobToTaskMap.TryRemove(jobId, out var taskId))
        {
            _taskToJobMap.TryRemove(taskId, out _);
            Log.Debug("已解除任务映射: JobId={JobId}", jobId);
        }
    }
    
    /// <summary>
    /// 检查任务是否存在
    /// </summary>
    public bool TaskExists(string taskId)
    {
        var cacheKey = GetTaskCacheKey(taskId);
        return _cache.TryGetValue(cacheKey, out _);
    }
    
    /// <summary>
    /// 获取所有活动任务ID
    /// </summary>
    public string[] GetActiveTaskIds()
    {
        return _taskToJobMap.Keys.ToArray();
    }
    
    /// <summary>
    /// 清理过期的映射关系
    /// </summary>
    public void CleanupExpiredMappings()
    {
        var expiredMappings = new List<string>();

        foreach (var kvp in _taskToJobMap)
        {
            if (!TaskExists(kvp.Key))
            {
                expiredMappings.Add(kvp.Key);
            }
        }

        foreach (var taskId in expiredMappings)
        {
            if (_taskToJobMap.TryRemove(taskId, out var jobId))
            {
                _jobToTaskMap.TryRemove(jobId, out _);
                Log.Debug("清理过期映射: TaskId={TaskId}", taskId);
            }
        }

        if (expiredMappings.Any())
        {
            Log.Information("已清理 {Count} 个过期的任务映射", expiredMappings.Count);
        }
    }

    #region 父任务子任务计数器管理

    /// <summary>
    /// 初始化父任务的子任务计数器
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <param name="childCount">子任务总数</param>
    public void InitializeParentCounter(string parentTaskId, int childCount)
    {
        _parentTaskCounters[parentTaskId] = childCount;
        Log.Debug("初始化父任务计数器: {ParentTaskId}, 子任务数: {ChildCount}",
            parentTaskId, childCount);
    }

    /// <summary>
    /// 原子递减计数器并检查是否所有子任务完成
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <returns>true 表示所有子任务已完成（计数器归零）</returns>
    public bool DecrementAndCheckCompletion(string parentTaskId)
    {
        if (!_parentTaskCounters.ContainsKey(parentTaskId))
        {
            // ✅ 容错：延迟子任务找不到计数器时不影响完成
            // 这是正常情况，因为新的轮询机制不依赖计数器
            Log.Debug("计数器不存在（可能父任务已使用轮询机制）: {ParentTaskId}",
                parentTaskId);
            return false; // 返回false表示不需要触发完成逻辑
        }

        // 使用 AddOrUpdate 进行线程安全的原子递减
        var remaining = _parentTaskCounters.AddOrUpdate(
            parentTaskId,
            0, // 如果键不存在，添加值为 0（实际不会走到这里，因为前面检查了）
            (key, oldValue) => Math.Max(0, oldValue - 1) // 原子递减，但不能小于0
        );

        Log.Debug("父任务计数器递减: {ParentTaskId}, 剩余子任务数: {Remaining}",
            parentTaskId, remaining);

        if (remaining == 0)
        {
            // 清理计数器
            _parentTaskCounters.TryRemove(parentTaskId, out _);
            Log.Information("父任务所有子任务已完成: {ParentTaskId}", parentTaskId);
            return true;
        }

        if (remaining < 0)
        {
            Log.Error("父任务计数器异常（小于0）: {ParentTaskId}, Remaining: {Remaining}",
                parentTaskId, remaining);
        }

        return false;
    }

    /// <summary>
    /// 获取父任务剩余子任务数
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <returns>剩余子任务数，如果未找到则返回0</returns>
    public int GetRemainingChildCount(string parentTaskId)
    {
        return _parentTaskCounters.TryGetValue(parentTaskId, out var count) ? count : 0;
    }

    /// <summary>
    /// 存储父任务的子任务Job ID列表（用于批量取消）
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <param name="childJobIds">子任务Job ID列表</param>
    public void StoreChildJobIds(string parentTaskId, List<string> childJobIds)
    {
        _parentToChildJobs[parentTaskId] = childJobIds;
        Log.Debug("存储父任务 {ParentTaskId} 的 {Count} 个子任务Job ID",
            parentTaskId, childJobIds.Count);
    }

    /// <summary>
    /// 获取父任务的子任务Job ID列表
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <returns>子任务Job ID列表，如果未找到则返回null</returns>
    public List<string>? GetChildJobIds(string parentTaskId)
    {
        return _parentToChildJobs.TryGetValue(parentTaskId, out var jobIds)
            ? jobIds
            : null;
    }

    #endregion

    #region 重试计数管理

    /// <summary>
    /// 获取任务的重试次数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>当前重试次数，如果未找到则返回0</returns>
    public int GetRetryCount(string taskId)
    {
        return _retryCountMap.TryGetValue(taskId, out var count) ? count : 0;
    }

    /// <summary>
    /// 增加任务的重试计数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>新的重试计数</returns>
    public int IncrementRetryCount(string taskId)
    {
        var newCount = _retryCountMap.AddOrUpdate(taskId, 1, (_, oldCount) => oldCount + 1);
        Log.Debug("任务重试计数增加: TaskId={TaskId}, NewCount={Count}", taskId, newCount);
        return newCount;
    }

    /// <summary>
    /// 重置任务的重试计数
    /// </summary>
    /// <param name="taskId">任务ID</param>
    public void ResetRetryCount(string taskId)
    {
        _retryCountMap.TryRemove(taskId, out _);
        Log.Debug("已重置任务重试计数: TaskId={TaskId}", taskId);
    }

    #endregion

    /// <summary>
    /// 获取缓存键
    /// </summary>
    private string GetTaskCacheKey(string taskId)
    {
        return $"task:metadata:{taskId}";
    }
    
    public void Dispose()
    {
        // 清理所有映射
        _jobToTaskMap.Clear();
        _taskToJobMap.Clear();
        _parentTaskCounters.Clear();
        _parentToChildJobs.Clear();
        _retryCountMap.Clear();

        Log.Debug("TaskMetadataStore 已释放");
    }
}