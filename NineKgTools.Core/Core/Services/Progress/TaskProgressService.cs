using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Progress;

/// <summary>
/// 任务进度服务 - 管理所有任务的进度报告
/// </summary>
public class TaskProgressService
{
    private readonly ConcurrentDictionary<string, TaskProgress> _taskProgressMap;
    private readonly ConcurrentDictionary<string, List<ITaskProgressObserver>> _observersMap;
    private readonly ConcurrentDictionary<string, TaskProgressReporter> _reportersMap;
    private readonly object _lockObject = new();
    
    public TaskProgressService()
    {
        _taskProgressMap = new ConcurrentDictionary<string, TaskProgress>();
        _observersMap = new ConcurrentDictionary<string, List<ITaskProgressObserver>>();
        _reportersMap = new ConcurrentDictionary<string, TaskProgressReporter>();
    }
    
    /// <summary>
    /// 创建任务进度报告器
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="taskName">任务名称</param>
    /// <param name="parentTaskId">父任务ID（可选）</param>
    public IProgressReporter CreateProgressReporter(string taskId, string taskName, string? parentTaskId = null, TaskType? taskType = null)
    {
        var reporter = new TaskProgressReporter(taskId, taskName, this);
        _reportersMap[taskId] = reporter;

        // 只在不存在时创建新的进度信息，避免覆盖已有的进度
        if (!_taskProgressMap.ContainsKey(taskId))
        {
            _taskProgressMap[taskId] = new TaskProgress
            {
                TaskId = taskId,
                TaskName = taskName,
                TaskType = taskType,
                Status = TaskExecutionStatus.Pending,
                StartTime = DateTime.UtcNow,
                ParentTaskId = parentTaskId
            };
        }

        return reporter;
    }
    
    /// <summary>
    /// 订阅任务进度
    /// </summary>
    public void Subscribe(string taskId, ITaskProgressObserver observer)
    {
        lock (_lockObject)
        {
            if (!_observersMap.ContainsKey(taskId))
            {
                _observersMap[taskId] = new List<ITaskProgressObserver>();
            }
            
            if (!_observersMap[taskId].Contains(observer))
            {
                _observersMap[taskId].Add(observer);
                Log.Debug("观察者已订阅任务进度: TaskId={TaskId}, Observer={ObserverName}", 
                    taskId, observer.Name);
            }
        }
    }
    
    /// <summary>
    /// 取消订阅任务进度
    /// </summary>
    public void Unsubscribe(string taskId, ITaskProgressObserver observer)
    {
        lock (_lockObject)
        {
            if (_observersMap.TryGetValue(taskId, out var observers))
            {
                observers.Remove(observer);
                Log.Debug("观察者已取消订阅: TaskId={TaskId}, Observer={ObserverName}", 
                    taskId, observer.Name);
                
                if (observers.Count == 0)
                {
                    _observersMap.TryRemove(taskId, out _);
                }
            }
        }
    }
    
    /// <summary>
    /// 获取任务进度
    /// </summary>
    public TaskProgress? GetProgress(string taskId)
    {
        return _taskProgressMap.TryGetValue(taskId, out var progress) ? progress : null;
    }

    /// <summary>
    /// 挂载识别诊断信息到指定任务的 TaskProgress，让运行中查询任务详情时也能拿到同一引用。
    /// 传入同一个 <see cref="IdentificationDiagnostics"/> 对象即可，因为诊断过程中都在原地修改。
    /// </summary>
    public void AttachDiagnostics(string taskId, IdentificationDiagnostics diagnostics)
    {
        if (_taskProgressMap.TryGetValue(taskId, out var progress))
        {
            progress.IdentificationDiagnostics = diagnostics;
        }
    }
    
    /// <summary>
    /// 获取所有活动任务的进度
    /// </summary>
    public IEnumerable<TaskProgress> GetAllActiveProgress()
    {
        return _taskProgressMap.Values
            .Where(p => p.Status == TaskExecutionStatus.Running || p.Status == TaskExecutionStatus.Pending)
            .OrderBy(p => p.StartTime)
            .ToList();
    }
    
    /// <summary>
    /// 获取所有任务进度（包括已完成的）
    /// </summary>
    public IEnumerable<TaskProgress> GetAllProgress()
    {
        return _taskProgressMap.Values.OrderBy(p => p.StartTime).ToList();
    }
    
    /// <summary>
    /// 更新任务进度
    /// </summary>
    internal async Task UpdateProgressAsync(string taskId, Action<TaskProgress> updateAction)
    {
        if (_taskProgressMap.TryGetValue(taskId, out var progress))
        {
            updateAction(progress);
            progress.LastUpdateTime = DateTime.UtcNow;

            // 通知所有观察者
            await NotifyObserversAsync(taskId, progress);

            // 如果是子任务，自动更新父任务的聚合进度
            if (!string.IsNullOrEmpty(progress.ParentTaskId))
            {
                await UpdateParentAggregatedProgressAsync(taskId);
            }
        }
    }
    
    /// <summary>
    /// 通知观察者
    /// </summary>
    private async Task NotifyObserversAsync(string taskId, TaskProgress progress)
    {
        if (_observersMap.TryGetValue(taskId, out var observers))
        {
            var tasks = observers.Select(observer => Task.Run(async () =>
            {
                try
                {
                    await observer.OnProgressUpdateAsync(progress);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "通知观察者时发生错误: {ObserverName}", observer.Name);
                }
            }));
            
            await Task.WhenAll(tasks);
        }
    }
    
    /// <summary>
    /// 清理已完成的任务进度
    /// </summary>
    public void CleanupCompletedTasks(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var toRemove = _taskProgressMap
            .Where(kvp => kvp.Value.EndTime.HasValue && kvp.Value.EndTime < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var taskId in toRemove)
        {
            _taskProgressMap.TryRemove(taskId, out _);
            _observersMap.TryRemove(taskId, out _);
            _reportersMap.TryRemove(taskId, out _);
        }
        
        Log.Debug("清理了 {Count} 个已完成的任务进度", toRemove.Count);
    }

    #region 树形进度查询和聚合

    /// <summary>
    /// 获取任务树（包括所有子任务）
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>任务进度树，如果任务不存在则返回 null</returns>
    public TaskProgress? GetTaskTree(string taskId)
    {
        var rootTask = GetProgress(taskId);
        if (rootTask == null) return null;

        // 递归加载所有子任务
        LoadChildTasks(rootTask);
        return rootTask;
    }

    /// <summary>
    /// 递归加载所有子任务
    /// </summary>
    private void LoadChildTasks(TaskProgress task)
    {
        // 查找所有父任务ID等于当前任务的子任务
        var children = _taskProgressMap.Values
            .Where(p => p.ParentTaskId == task.TaskId)
            .OrderBy(p => p.StartTime)
            .ToList();

        task.ChildTasks.Clear();
        task.ChildTasks.AddRange(children);

        // 递归加载孙任务
        foreach (var child in children)
        {
            LoadChildTasks(child);
        }
    }

    /// <summary>
    /// 获取所有根任务（没有父任务的任务）
    /// </summary>
    /// <returns>根任务列表</returns>
    public IEnumerable<TaskProgress> GetAllRootTasks()
    {
        return _taskProgressMap.Values
            .Where(p => string.IsNullOrEmpty(p.ParentTaskId))
            .OrderBy(p => p.StartTime)
            .Select(p =>
            {
                var root = GetProgress(p.TaskId);
                if (root != null)
                {
                    LoadChildTasks(root);
                }
                return root;
            })
            .Where(p => p != null)
            .Cast<TaskProgress>()
            .ToList();
    }

    /// <summary>
    /// 更新父任务的聚合进度
    /// 当子任务进度更新时，自动触发父任务进度的聚合计算
    /// </summary>
    /// <param name="taskId">子任务ID</param>
    public async Task UpdateParentAggregatedProgressAsync(string taskId)
    {
        var task = GetProgress(taskId);
        if (task == null || string.IsNullOrEmpty(task.ParentTaskId)) return;

        var parentTask = GetProgress(task.ParentTaskId);
        if (parentTask == null) return;

        // 重新加载所有子任务并计算聚合进度
        var siblings = _taskProgressMap.Values
            .Where(p => p.ParentTaskId == task.ParentTaskId)
            .ToList();

        if (siblings.Count == 0) return;

        // 计算聚合进度（所有子任务的平均进度）
        var aggregatedProgress = siblings.Average(s => s.ProgressPercentage);

        // 计算总处理项数和失败项数
        var totalProcessed = siblings.Sum(s => s.ProcessedItems);
        var totalFailed = siblings.Sum(s => s.FailedItems);

        await UpdateProgressAsync(task.ParentTaskId, p =>
        {
            p.ProgressPercentage = aggregatedProgress;
            p.ProcessedItems = totalProcessed;
            p.FailedItems = totalFailed;

            // 计算各状态的子任务数量
            var successCount = siblings.Count(s => s.Status == TaskExecutionStatus.Succeeded);
            var failedCount = siblings.Count(s => s.Status == TaskExecutionStatus.Failed);
            var runningCount = siblings.Count(s => s.Status == TaskExecutionStatus.Running);
            var completedCount = successCount + failedCount;

            // 更新父任务状态
            // 如果所有子任务都完成，父任务也标记为完成
            var allSucceeded = siblings.All(s => s.Status == TaskExecutionStatus.Succeeded);
            var allCompleted = siblings.All(s =>
                s.Status == TaskExecutionStatus.Succeeded ||
                s.Status == TaskExecutionStatus.Failed ||
                s.Status == TaskExecutionStatus.Cancelled);

            if (allCompleted)
            {
                p.Status = allSucceeded ? TaskExecutionStatus.Succeeded : TaskExecutionStatus.Failed;
                p.EndTime = DateTime.UtcNow;

                // 更新完成消息
                p.CurrentMessage = $"所有子任务已完成: 成功 {successCount}/{siblings.Count}";
                if (failedCount > 0)
                {
                    p.CurrentMessage += $", 失败 {failedCount}";
                }
            }
            else if (siblings.Any(s => s.Status == TaskExecutionStatus.Running))
            {
                p.Status = TaskExecutionStatus.Running;

                // 更新运行中的进度消息
                p.CurrentMessage = $"正在执行: {completedCount}/{siblings.Count} 个子任务已完成 (运行中: {runningCount})";
            }
        });

        // 递归更新祖父任务
        if (!string.IsNullOrEmpty(parentTask.ParentTaskId))
        {
            await UpdateParentAggregatedProgressAsync(task.ParentTaskId);
        }
    }

    /// <summary>
    /// 设置任务的父任务ID（用于建立父子关系）
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="parentTaskId">父任务ID</param>
    public async Task SetParentTaskAsync(string taskId, string? parentTaskId)
    {
        await UpdateProgressAsync(taskId, p => p.ParentTaskId = parentTaskId);
    }

    #endregion

    /// <summary>
    /// 内部进度报告器实现
    /// </summary>
    private class TaskProgressReporter : IProgressReporter
    {
        private readonly string _taskId;
        private readonly string _taskName;
        private readonly TaskProgressService _service;

        public TaskProgressReporter(string taskId, string taskName, TaskProgressService service)
        {
            _taskId = taskId;
            _taskName = taskName;
            _service = service;
        }

        #region 核心统一方法实现

        public async Task ReportAsync(
            string message,
            double? progress = null,
            TaskLogLevel level = TaskLogLevel.Info,
            string? currentItem = null,
            string? phase = null,
            object? extraData = null)
        {
            await _service.UpdateProgressAsync(_taskId, p =>
            {
                // 更新进度（如果提供）
                if (progress.HasValue)
                {
                    p.ProgressPercentage = progress.Value;
                    p.Status = TaskExecutionStatus.Running;

                    // 估算剩余时间
                    if (p.StartTime != null && progress.Value > 0 && progress.Value < 100)
                    {
                        var elapsed = DateTime.UtcNow - p.StartTime.Value;
                        var estimatedTotal = elapsed.TotalSeconds / (progress.Value / 100);
                        p.EstimatedTimeRemaining = TimeSpan.FromSeconds(estimatedTotal - elapsed.TotalSeconds);
                    }
                }

                // 更新阶段（如果提供）
                if (phase != null)
                {
                    p.CurrentPhase = phase;
                }

                // 更新消息和当前项
                p.CurrentMessage = message;
                if (currentItem != null)
                {
                    p.CurrentItem = currentItem;
                }

                // 创建日志条目
                var entry = new TaskLogEntry
                {
                    Message = message,
                    Level = level,
                    CurrentItem = currentItem,
                    Progress = progress ?? p.ProgressPercentage
                };

                // 添加到日志缓冲区
                p.AddLogEntry(entry);
            });
        }

        #endregion

        #region 便捷方法实现

        public Task InfoAsync(string message, double progress, string? currentItem = null)
            => ReportAsync(message, progress, TaskLogLevel.Info, currentItem);

        public Task WarningAsync(string message, double? progress = null, string? currentItem = null)
            => ReportAsync(message, progress, TaskLogLevel.Warning, currentItem);

        public Task ErrorAsync(string message, double? progress = null, string? currentItem = null)
            => ReportAsync(message, progress, TaskLogLevel.Error, currentItem);

        public Task SuccessAsync(string message, double? progress = null, string? currentItem = null)
            => ReportAsync(message, progress, TaskLogLevel.Success, currentItem);

        public Task DebugAsync(string message, string? currentItem = null)
            => ReportAsync(message, null, TaskLogLevel.Debug, currentItem);

        #endregion

        #region 生命周期方法实现

        public async Task StartAsync(string message, int? totalItems = null)
        {
            await _service.UpdateProgressAsync(_taskId, p =>
            {
                p.Status = TaskExecutionStatus.Running;
                p.StartTime = DateTime.UtcNow;
                if (totalItems.HasValue)
                {
                    p.TotalItems = totalItems.Value;
                }
                p.CurrentMessage = message;
                p.ProgressPercentage = 0;

                // 添加日志条目
                p.AddLogEntry(new TaskLogEntry
                {
                    Message = message,
                    Level = TaskLogLevel.Info,
                    Progress = 0
                });
            });

            Log.Information("任务已开始: {TaskName} ({TaskId})", _taskName, _taskId);
        }

        public async Task PhaseAsync(string phase, double progress, string? message = null)
        {
            await _service.UpdateProgressAsync(_taskId, p =>
            {
                p.CurrentPhase = phase;
                p.ProgressPercentage = progress;
                p.CurrentMessage = message ?? phase;

                // 添加日志条目
                p.AddLogEntry(new TaskLogEntry
                {
                    Message = message ?? phase,
                    Level = TaskLogLevel.Info,
                    Progress = progress
                });
            });
        }

        public async Task CompleteAsync(string message, int? processedItems = null, int? failedItems = null)
        {
            await _service.UpdateProgressAsync(_taskId, p =>
            {
                p.Status = TaskExecutionStatus.Succeeded;
                p.EndTime = DateTime.UtcNow;
                p.CurrentMessage = message;
                if (processedItems.HasValue)
                {
                    p.ProcessedItems = processedItems.Value;
                }
                if (failedItems.HasValue)
                {
                    p.FailedItems = failedItems.Value;
                }
                p.ProgressPercentage = 100;
                p.EstimatedTimeRemaining = TimeSpan.Zero;

                // 添加日志条目
                p.AddLogEntry(new TaskLogEntry
                {
                    Message = message,
                    Level = TaskLogLevel.Success,
                    Progress = 100
                });
            });

            var duration = _service.GetProgress(_taskId)?.Duration;
            Log.Information("任务已完成: {TaskName} ({TaskId}), 耗时: {Duration:F2}s",
                _taskName, _taskId, duration?.TotalSeconds ?? 0);
        }

        public async Task FailAsync(string error, Exception? exception = null)
        {
            await _service.UpdateProgressAsync(_taskId, p =>
            {
                p.Status = TaskExecutionStatus.Failed;
                p.EndTime = DateTime.UtcNow;
                p.CurrentMessage = error;
                p.ErrorMessage = error;

                // 添加日志条目
                p.AddLogEntry(new TaskLogEntry
                {
                    Message = error,
                    Level = TaskLogLevel.Error
                });
            });

            if (exception != null)
            {
                Log.Error(exception, "任务执行失败: {TaskName} ({TaskId})", _taskName, _taskId);
            }
            else
            {
                Log.Warning("任务执行失败: {TaskName} ({TaskId}): {Error}", _taskName, _taskId, error);
            }
        }

        #endregion
    }
}

/// <summary>
/// 任务进度观察者接口
/// </summary>
public interface ITaskProgressObserver
{
    string Name { get; }
    Task OnProgressUpdateAsync(TaskProgress progress);
}