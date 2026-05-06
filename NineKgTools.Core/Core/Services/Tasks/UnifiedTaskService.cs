using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Tasks.ScheduledTasks;
using Serilog;

namespace NineKgTools.Core.Services.Tasks;

/// <summary>
/// 统一的任务服务 - 整合 Hangfire 和 ITask 接口
/// </summary>
public class UnifiedTaskService : IDisposable
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly TaskProgressService _progressService;
    private readonly TaskMetadataStore _metadataStore;
    private readonly ScheduledTaskFactory _taskFactory;
    private readonly Config _config;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    // 任务执行历史
    private readonly ConcurrentDictionary<string, TaskExecutionInfo> _executionHistory;
    private readonly ConcurrentDictionary<string, ScheduledTaskConfig> _scheduledTaskConfigs;
    private readonly object _statsLock = new();

    /// <summary>
    /// 已在执行中的任务 ID 集合 — 进程内互斥，防止同一 taskId 被 Hangfire 多 worker
    /// 重复 fetch 后并发执行。Hangfire.SQLite 1.4.2 在高 WorkerCount 下 FetchNextJob
    /// 行锁不可靠，单 jobId 可能被多 worker 同时拾取，每个都会进入 ExecuteTaskAsync /
    /// ExecuteParentTaskAsync —— 父任务尤其严重，每次重复执行都会再 enqueue 全部子任务，
    /// 导致 N×N 雪崩。这里用 ConcurrentDictionary 守门：同一 taskId 进入时第二次会直接 return。
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> _runningTaskIds = new();

    public UnifiedTaskService(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager,
        TaskProgressService progressService,
        TaskMetadataStore metadataStore,
        ScheduledTaskFactory taskFactory,
        Config config,
        IServiceScopeFactory serviceScopeFactory)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
        _progressService = progressService;
        _metadataStore = metadataStore;
        _taskFactory = taskFactory;
        _config = config;
        _serviceScopeFactory = serviceScopeFactory;
        _executionHistory = new ConcurrentDictionary<string, TaskExecutionInfo>();
        _scheduledTaskConfigs = new ConcurrentDictionary<string, ScheduledTaskConfig>();

        // 初始化定时任务配置
        InitializeScheduledTaskConfigs();
    }
    
    #region 即时任务管理
    
    /// <summary>
    /// 提交任务到队列
    /// </summary>
    public async Task<string> SubmitTaskAsync(ITask task)
    {
        // 验证任务参数
        if (!task.ValidateParameters())
        {
            Log.Warning("任务参数验证失败: {TaskName}", task.TaskName);
            throw new ArgumentException($"任务参数验证失败: {task.TaskName}");
        }
        
        // 存储任务元数据
        await _metadataStore.StoreTaskAsync(task);

        // 根据任务类型和优先级获取队列名称
        var queueName = GetQueueName(task);

        // Hangfire.MemoryStorage 不支持 Enqueue<T>(queue, expr) 动态指定队列
        // （会抛 "Current storage doesn't support specifying queues directly"），
        // 只能通过方法上的 [Queue(...)] 特性指定。为此每个队列都有一个包装方法，
        // 按 queueName 分发到对应方法让 Hangfire 读到正确的 [Queue] 特性。
        var jobId = EnqueueTaskByQueue(queueName, task.TaskId);
        
        // 记录任务信息
        _metadataStore.MapJobToTask(jobId, task.TaskId);
        
        Log.Information("任务已提交: {TaskName} (ID: {TaskId}, JobId: {JobId}, Queue: {Queue})", 
            task.TaskName, task.TaskId, jobId, queueName);
        
        return jobId;
    }
    
    /// <summary>
    /// 立即执行任务（跳过队列）
    /// </summary>
    public async Task<TaskResult> ExecuteTaskImmediatelyAsync(ITask task, CancellationToken cancellationToken = default)
    {
        // 验证任务参数
        if (!task.ValidateParameters())
        {
            Log.Warning("任务参数验证失败: {TaskName}", task.TaskName);
            return TaskResult.CreateFailure(task.TaskId, task.TaskName, "任务参数验证失败");
        }
        
        // 直接执行任务
        return await ExecuteTaskInternalAsync(task, cancellationToken);
    }
    
    /// <summary>
    /// Hangfire 执行入口
    /// </summary>
    [Queue("default")] // 默认队列，实际会被动态覆盖
    [AutomaticRetry(Attempts = 0)] // 禁用Hangfire自动重试，使用自定义重试逻辑
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteTaskAsync(string taskId, PerformContext? context)
    {
        // 进程内互斥：防 Hangfire SQLite 多 worker 拾取同一 jobId 导致重复执行
        if (!_runningTaskIds.TryAdd(taskId, 0))
        {
            Log.Warning("任务 {TaskId} 已在执行中，跳过本次重复调度（Hangfire fetch 竞态防护）", taskId);
            return;
        }

        try
        {
        // 从元数据存储获取任务
        var task = await _metadataStore.GetTaskAsync(taskId);
        if (task == null)
        {
            // 优雅返回而非抛异常——孤儿 job 不让 Hangfire retry
            Log.Warning("未找到任务元数据: {TaskId}（孤儿 job，跳过）", taskId);
            return;
        }

        // 获取取消令牌
        var cancellationToken = context?.CancellationToken.ShutdownToken ?? CancellationToken.None;

        // 获取重试配置
        var maxRetries = _config.Tasks?.RetryCount ?? 3;
        var currentRetry = _metadataStore.GetRetryCount(taskId);

        // 标记是否需要在 finally 中清理元数据
        // 默认为 true，只有当需要重试时设置为 false
        var shouldCleanup = true;

        try
        {
            // 如果是重试执行，更新进度状态
            if (currentRetry > 0)
            {
                await _progressService.UpdateProgressAsync(taskId, p =>
                {
                    p.Status = TaskExecutionStatus.Retrying;
                    p.CurrentRetry = currentRetry;
                    p.MaxRetries = maxRetries;
                    p.CurrentMessage = $"正在重试 ({currentRetry}/{maxRetries})...";
                });

                Log.Information("开始重试任务 ({CurrentRetry}/{MaxRetries}): {TaskName}",
                    currentRetry, maxRetries, task.TaskName);
            }

            // 执行任务
            var result = await ExecuteTaskInternalAsync(task, cancellationToken);

            if (!result.Success)
            {
                // 任务失败，检查是否需要重试
                if (currentRetry < maxRetries)
                {
                    shouldCleanup = false; // 需要重试，不清理元数据
                    await ScheduleRetryAsync(taskId, task, result.Message, currentRetry, maxRetries);
                }
                else
                {
                    // 已达到最大重试次数，更新失败状态
                    await _progressService.UpdateProgressAsync(taskId, p =>
                    {
                        p.Status = TaskExecutionStatus.Failed;
                        p.ErrorMessage = $"任务失败（已重试 {maxRetries} 次）: {result.Message}";
                        p.CurrentMessage = p.ErrorMessage;
                        p.EndTime = DateTime.UtcNow;
                    });
                }
            }
            // 成功时 shouldCleanup 保持 true，在 finally 中清理
        }
        catch (OperationCanceledException)
        {
            // 任务被取消，不重试，shouldCleanup 保持 true
            Log.Warning("任务被取消: {TaskName} (ID: {TaskId})", task.TaskName, taskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "任务执行异常: {TaskName} (ID: {TaskId})", task.TaskName, taskId);

            // 检查是否需要重试
            if (currentRetry < maxRetries)
            {
                shouldCleanup = false; // 需要重试，不清理元数据
                await ScheduleRetryAsync(taskId, task, ex.Message, currentRetry, maxRetries);
            }
            else
            {
                // 已达到最大重试次数，更新失败状态
                await _progressService.UpdateProgressAsync(taskId, p =>
                {
                    p.Status = TaskExecutionStatus.Failed;
                    p.ErrorMessage = $"任务失败（已重试 {maxRetries} 次）: {ex.Message}";
                    p.CurrentMessage = p.ErrorMessage;
                    p.EndTime = DateTime.UtcNow;
                });
            }
        }
        finally
        {
            // 根据标志决定是否清理元数据
            // 只有任务成功、被取消、或重试次数用尽时才清理
            if (shouldCleanup)
            {
                await CleanupTaskMetadataAsync(taskId, context?.BackgroundJob?.Id);
            }
        }
        }
        finally
        {
            // 进程内互斥锁释放——无论成功 / 失败 / 重试，都让同 taskId 后续可再次执行
            _runningTaskIds.TryRemove(taskId, out _);
        }
    }

    /// <summary>
    /// 调度任务重试
    /// </summary>
    private async Task ScheduleRetryAsync(
        string taskId,
        ITask task,
        string? errorMessage,
        int currentRetry,
        int maxRetries)
    {
        var newRetryCount = _metadataStore.IncrementRetryCount(taskId);

        await _progressService.UpdateProgressAsync(taskId, p =>
        {
            p.Status = TaskExecutionStatus.Retrying;
            p.CurrentRetry = newRetryCount;
            p.MaxRetries = maxRetries;
            p.CurrentMessage = $"任务失败，准备第 {newRetryCount} 次重试...";
        });

        // 计算指数退避延迟（2^n * 5秒）
        var delay = TimeSpan.FromSeconds(Math.Pow(2, currentRetry) * 5);

        Log.Warning("任务失败，将在 {Delay} 后重试 ({NewRetry}/{MaxRetries}): {TaskName} - {Error}",
            delay, newRetryCount, maxRetries, task.TaskName, errorMessage);

        // 调度重试任务
        _backgroundJobClient.Schedule<UnifiedTaskService>(
            service => service.ExecuteTaskAsync(taskId, null), delay);
    }

    /// <summary>
    /// 清理任务元数据
    /// </summary>
    private async Task CleanupTaskMetadataAsync(string taskId, string? jobId)
    {
        await _metadataStore.RemoveTaskAsync(taskId);

        if (!string.IsNullOrEmpty(jobId))
        {
            _metadataStore.UnmapJob(jobId);
        }

        Log.Debug("已清理任务元数据: TaskId={TaskId}", taskId);
    }
    
    /// <summary>
    /// 内部执行任务
    /// </summary>
    private async Task<TaskResult> ExecuteTaskInternalAsync(ITask task, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // 创建进度报告器
            var progressReporter = _progressService.CreateProgressReporter(task.TaskId, task.TaskName, task.ParentTaskId);

            // 调用任务执行前钩子
            await task.OnBeforeExecuteAsync(cancellationToken);

            // 执行任务
            Log.Information("开始执行任务: {TaskName} (ID: {TaskId})", task.TaskName, task.TaskId);
            var result = await task.ExecuteAsync(progressReporter, cancellationToken);

            // 设置执行时间和任务类型
            result.StartTime = startTime;
            result.EndTime = DateTime.UtcNow;
            result.TaskType = task.TaskType;

            // 调用任务执行后钩子
            await task.OnAfterExecuteAsync(result, cancellationToken);

            // 更新执行历史（在通知父任务之前，确保最后一个子任务的结果也被记录）
            UpdateExecutionHistory(task.TaskId, result);

            // 如果是子任务，通知父任务
            await NotifyParentTaskOnChildCompleteAsync(task, result);

            Log.Information("任务执行完成: {TaskName} (ID: {TaskId}, Success: {Success})",
                task.TaskName, task.TaskId, result.Success);

            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Warning("任务被取消: {TaskName} (ID: {TaskId})", task.TaskName, task.TaskId);

            var result = TaskResult.CreateFailure(task.TaskId, task.TaskName, "任务被取消", taskType: task.TaskType);
            result.StartTime = startTime;
            result.EndTime = DateTime.UtcNow;

            // 更新执行历史（在通知父任务之前）
            UpdateExecutionHistory(task.TaskId, result);

            // 即使任务被取消，也通知父任务
            await NotifyParentTaskOnChildCompleteAsync(task, result);

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "任务执行失败: {TaskName} (ID: {TaskId})", task.TaskName, task.TaskId);

            var failResult = TaskResult.CreateFailure(task.TaskId, task.TaskName, ex.Message, taskType: task.TaskType);
            failResult.StartTime = startTime;
            failResult.EndTime = DateTime.UtcNow;

            // 更新执行历史（在通知父任务之前）
            UpdateExecutionHistory(task.TaskId, failResult);

            // 即使任务失败，也通知父任务
            await NotifyParentTaskOnChildCompleteAsync(task, failResult);

            return failResult;
        }
    }
    
    /// <summary>
    /// 取消任务
    /// </summary>
    public async Task<bool> CancelTaskAsync(string jobId)
    {
        try
        {
            // 从 Hangfire 删除任务
            var deleted = _backgroundJobClient.Delete(jobId);
            
            // 获取关联的任务ID
            var taskId = _metadataStore.GetTaskIdByJobId(jobId);
            if (!string.IsNullOrEmpty(taskId))
            {
                // 清理元数据
                await _metadataStore.RemoveTaskAsync(taskId);
                _metadataStore.UnmapJob(jobId);
                
                // 进度已通过其他方式更新
            }
            
            Log.Information("任务已取消: JobId: {JobId}", jobId);
            return deleted;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消任务失败: JobId: {JobId}", jobId);
            return false;
        }
    }
    
    #region 父子任务管理

    /// <summary>
    /// 提交父任务（自动创建子任务并并发执行）
    /// </summary>
    /// <param name="parentTask">父任务</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>父任务的Job ID</returns>
    public async Task<string> SubmitParentTaskAsync(IParentTask parentTask, CancellationToken cancellationToken = default)
    {
        // 验证父任务参数
        if (!parentTask.ValidateParameters())
        {
            Log.Warning("父任务参数验证失败: {TaskName}", parentTask.TaskName);
            throw new ArgumentException($"父任务参数验证失败: {parentTask.TaskName}");
        }

        // 存储父任务元数据
        await _metadataStore.StoreTaskAsync(parentTask);

        // 创建父任务的进度报告器
        var parentProgressReporter = _progressService.CreateProgressReporter(parentTask.TaskId, parentTask.TaskName);
        await parentProgressReporter.StartAsync("父任务已提交", 0);

        // 直接Enqueue父任务Job（父任务内部会创建和管理子任务）
        // 按任务类型和优先级路由队列，避免全部落到 default（经由 EnqueueParentTaskByQueue 分发到带 [Queue] 特性的包装方法）
        var parentQueueName = GetQueueName(parentTask);
        var parentJobId = EnqueueParentTaskByQueue(parentQueueName, parentTask.TaskId);

        // 建立映射：父任务TaskId → 父任务JobId
        _metadataStore.MapJobToTask(parentJobId, parentTask.TaskId);

        Log.Information("父任务已提交: {TaskName} (TaskId: {TaskId}, JobId: {JobId})",
            parentTask.TaskName, parentTask.TaskId, parentJobId);

        // 返回父任务的Job ID
        return parentJobId;
    }

    /// <summary>
    /// 执行父任务的主逻辑（新架构）
    /// </summary>
    [Queue("default")]
    [JobDisplayName("父任务: {0}")]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ExecuteParentTaskAsync(string parentTaskId, PerformContext? context)
    {
        // 进程内互斥：防 Hangfire SQLite 多 worker 拾取同一 jobId 导致父任务重复执行。
        // 父任务尤其关键 — 每次重复执行都会 CreateChildTasksAsync + 全量 enqueue 子任务，
        // 如果重复 N 次会产生 N×ChildCount 的子任务雪崩（实测 60×7=420 个孤儿子任务）。
        var taskKey = $"parent:{parentTaskId}";
        if (!_runningTaskIds.TryAdd(taskKey, 0))
        {
            Log.Warning("父任务 {Id} 已在执行中，跳过本次重复调度（Hangfire fetch 竞态防护）", parentTaskId);
            return;
        }

        try
        {
        var parentTask = await _metadataStore.GetTaskAsync(parentTaskId) as IParentTask;
        if (parentTask == null)
        {
            // 优雅返回——孤儿父任务不让 Hangfire retry
            Log.Warning("未找到父任务元数据: {ParentTaskId}（孤儿 job，跳过）", parentTaskId);
            return;
        }

        var cancellationToken = context?.CancellationToken.ShutdownToken ?? CancellationToken.None;
        var reporter = _progressService.CreateProgressReporter(parentTaskId, parentTask.TaskName);

        try
        {
            Log.Information("开始执行父任务: {TaskName} (ID: {TaskId})",
                parentTask.TaskName, parentTaskId);

            // Phase 1: 调用前置钩子
            await reporter.PhaseAsync("初始化", 0);
            await parentTask.OnBeforeExecuteAsync(cancellationToken);

            // Phase 2: 创建子任务
            await reporter.PhaseAsync("创建子任务", 10);
            var childTasks = await parentTask.CreateChildTasksAsync();

            if (childTasks.Count == 0)
            {
                await reporter.CompleteAsync("没有需要执行的子任务");
                return;
            }

            Log.Information("父任务已创建 {Count} 个子任务: {TaskName}",
                childTasks.Count, parentTask.TaskName);

            // Phase 3: 提交子任务到Hangfire（使用普通Enqueue，不再使用ContinueJobWith）
            await reporter.PhaseAsync("提交子任务", 30);
            var childJobIds = new List<string>();

            foreach (var childTask in childTasks)
            {
                // 设置父子关系
                childTask.ParentTaskId = parentTaskId;
                await _metadataStore.StoreTaskAsync(childTask);

                // 使用普通Enqueue，立即调度 —— 子任务按自身类型路由队列
                // 批量识别的子任务是 SingleSourceIdentificationTask（IIdentificationTask），
                // 会路由到 identification 队列受并发数限制。
                var childQueueName = GetQueueName(childTask);
                var childJobId = EnqueueTaskByQueue(childQueueName, childTask.TaskId);

                childJobIds.Add(childJobId);
                _metadataStore.MapJobToTask(childJobId, childTask.TaskId);
                parentTask.ChildTaskIds.Add(childTask.TaskId);

                Log.Debug("子任务已提交: {ChildTaskName} (JobId: {JobId})",
                    childTask.TaskName, childJobId);
            }

            // 存储子任务Job ID列表（用于取消）
            _metadataStore.StoreChildJobIds(parentTaskId, childJobIds);

            Log.Information("父任务所有子任务已提交: {TaskName}, 子任务数: {Count}",
                parentTask.TaskName, childJobIds.Count);

            // Phase 4: 轮询等待所有子任务完成
            await reporter.PhaseAsync("等待子任务完成", 40);
            await WaitForChildTasksCompletionAsync(parentTaskId, childJobIds, reporter, cancellationToken);

            // Phase 5: 执行完成回调
            await reporter.PhaseAsync("执行完成回调", 90);
            await ExecuteParentTaskFinalizationInternalAsync(parentTaskId, parentTask, cancellationToken);

            await reporter.CompleteAsync("父任务执行完成");
            Log.Information("父任务执行完成: {TaskName} (ID: {TaskId})",
                parentTask.TaskName, parentTaskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "父任务执行失败: {TaskName} (ID: {TaskId})",
                parentTask.TaskName, parentTaskId);
            await reporter.FailAsync($"父任务执行失败: {ex.Message}", ex);
            throw;
        }
        finally
        {
            // 清理父任务元数据
            await _metadataStore.RemoveTaskAsync(parentTaskId);
        }
        }
        finally
        {
            // 释放进程内互斥锁
            _runningTaskIds.TryRemove(taskKey, out _);
        }
    }

    /// <summary>
    /// 轮询等待所有子任务完成
    /// </summary>
    private async Task WaitForChildTasksCompletionAsync(
        string parentTaskId,
        List<string> childJobIds,
        IProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        var checkInterval = TimeSpan.FromSeconds(2);
        var maxWaitTime = TimeSpan.FromHours(24); // 最长等待24小时
        var startTime = DateTime.UtcNow;

        Log.Information("开始轮询等待子任务完成: ParentTaskId={ParentTaskId}, ChildCount={Count}",
            parentTaskId, childJobIds.Count);

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 检查所有子任务的Job状态
            var completedCount = 0;
            var failedCount = 0;
            var cancelledCount = 0;

            foreach (var jobId in childJobIds)
            {
                try
                {
                    var jobData = JobStorage.Current.GetConnection().GetJobData(jobId);

                    if (jobData?.State == "Succeeded")
                    {
                        completedCount++;
                    }
                    else if (jobData?.State == "Failed")
                    {
                        failedCount++;
                    }
                    else if (jobData?.State == "Deleted")
                    {
                        cancelledCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "获取子任务Job状态失败: JobId={JobId}", jobId);
                }
            }

            var totalCount = childJobIds.Count;
            var finishedCount = completedCount + failedCount + cancelledCount;
            var progress = 40 + (finishedCount * 50.0 / totalCount); // 40-90%的进度范围

            var message = $"已完成 {completedCount}/{totalCount} 个子任务";
            if (failedCount > 0)
                message += $", 失败 {failedCount}";
            if (cancelledCount > 0)
                message += $", 已取消 {cancelledCount}";

            await reporter.InfoAsync(message, progress);

            // 所有子任务都完成（成功、失败或取消）
            if (finishedCount >= totalCount)
            {
                Log.Information("所有子任务已完成: ParentTaskId={ParentTaskId}, 成功: {Succeeded}, 失败: {Failed}, 取消: {Cancelled}",
                    parentTaskId, completedCount, failedCount, cancelledCount);
                break;
            }

            // 等待下一次检查
            await Task.Delay(checkInterval, cancellationToken);
        }

        if (DateTime.UtcNow - startTime >= maxWaitTime)
        {
            Log.Warning("父任务等待子任务超时: ParentTaskId={ParentTaskId}, MaxWaitTime={MaxWaitTime}",
                parentTaskId, maxWaitTime);
            throw new TimeoutException($"等待子任务完成超时 (>{maxWaitTime.TotalHours}小时)");
        }
    }

    /// <summary>
    /// 父任务完成回调的内部实现（非Hangfire Job）
    /// </summary>
    private async Task ExecuteParentTaskFinalizationInternalAsync(
        string parentTaskId,
        IParentTask parentTask,
        CancellationToken cancellationToken)
    {
        try
        {
            Log.Information("开始执行父任务后置处理: {TaskName} (ID: {TaskId})",
                parentTask.TaskName, parentTaskId);

            // 收集所有子任务的执行结果
            var childResults = new List<TaskResult>();
            foreach (var childTaskId in parentTask.ChildTaskIds)
            {
                if (_executionHistory.TryGetValue(childTaskId, out var childExecInfo))
                {
                    var childResult = new TaskResult
                    {
                        TaskId = childTaskId,
                        TaskName = childExecInfo.TaskName,
                        Success = childExecInfo.Success,
                        Message = childExecInfo.Message,
                        ProcessedItems = childExecInfo.ProcessedItems,
                        FailedItems = childExecInfo.FailedItems,
                        StartTime = childExecInfo.StartTime,
                        EndTime = childExecInfo.EndTime
                    };
                    childResults.Add(childResult);
                }
            }

            // 调用父任务的所有子任务完成回调
            await parentTask.OnAllChildTasksCompletedAsync(childResults);

            // 统计结果
            var totalSuccess = childResults.Count(r => r.Success);
            var totalFailed = childResults.Count - totalSuccess;

            // 创建父任务的执行结果
            var parentResult = totalFailed == 0
                ? TaskResult.CreateSuccess(parentTaskId, parentTask.TaskName,
                    $"所有子任务执行成功 ({totalSuccess}/{childResults.Count})", totalSuccess)
                : TaskResult.CreateFailure(parentTaskId, parentTask.TaskName,
                    $"部分子任务失败 ({totalFailed}/{childResults.Count})", failedCount: totalFailed);

            if (childResults.Any())
            {
                parentResult.StartTime = childResults.Min(r => r.StartTime);
                parentResult.EndTime = DateTime.UtcNow;
            }

            // 更新父任务执行历史
            UpdateExecutionHistory(parentTaskId, parentResult);

            // 调用父任务的后置钩子
            await parentTask.OnAfterExecuteAsync(parentResult, cancellationToken);

            Log.Information("父任务后置处理完成: {TaskName}, 成功: {Success}/{Total}, 失败: {Failed}",
                parentTask.TaskName, totalSuccess, childResults.Count, totalFailed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "父任务后置处理失败: {TaskName}", parentTask.TaskName);
            throw;
        }
    }

    /// <summary>
    /// 修改 ExecuteTaskInternalAsync 以支持子任务完成时通知父任务
    /// </summary>
    private async Task NotifyParentTaskOnChildCompleteAsync(ITask task, TaskResult result)
    {
        if (string.IsNullOrEmpty(task.ParentTaskId))
        {
            return; // 不是子任务，无需通知
        }

        var parentTask = await _metadataStore.GetTaskAsync(task.ParentTaskId) as IParentTask;
        if (parentTask == null)
        {
            Log.Warning("未找到父任务元数据: {ParentTaskId}", task.ParentTaskId);
            return;
        }

        try
        {
            // 调用父任务的单个子任务完成回调
            await parentTask.OnChildTaskCompletedAsync(task.TaskId, result);
            Log.Debug("已通知父任务 {ParentTaskId} 子任务 {ChildTaskId} 完成",
                task.ParentTaskId, task.TaskId);

            // 原子递减计数器（兼容旧架构，新架构使用轮询机制不依赖此计数器）
            var allCompleted = _metadataStore.DecrementAndCheckCompletion(task.ParentTaskId);

            if (allCompleted)
            {
                Log.Debug("计数器归零（旧架构），但新架构使用轮询机制，父任务会自动检测完成: {ParentTaskId}",
                    task.ParentTaskId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "通知父任务时发生错误: ParentTaskId={ParentTaskId}, ChildTaskId={ChildTaskId}",
                task.ParentTaskId, task.TaskId);
        }
    }

    #endregion

    #endregion
    
    #region 定时任务管理

    /// <summary>
    /// 执行定时任务（通过任务键名）
    /// </summary>
    public async Task ExecuteScheduledTaskAsync(string taskKey, CancellationToken cancellationToken)
    {
        // 1. 尝试从工厂创建任务实例（使用键名，支持自动发现的任务）
        var task = _taskFactory.CreateTask(taskKey) as IScheduledTask;
        if (task == null)
        {
            Log.Error("无法创建任务实例: {TaskKey}", taskKey);
            return;
        }

        // 2. 尝试从配置中获取任务配置（获取参数等）
        if (_scheduledTaskConfigs.TryGetValue(taskKey, out var config))
        {
            // 设置任务参数
            if (config.Parameters != null)
            {
                task.Parameters = config.Parameters;
            }
        }

        // 3. 执行任务
        var result = await ExecuteTaskImmediatelyAsync(task, cancellationToken);

        // 4. 更新定时任务历史（使用任务的中文名称而非键名）
        UpdateScheduledTaskHistory(result.TaskName, result);
    }

    #endregion
    
    #region 任务监控与统计
    
    /// <summary>
    /// 获取运行中的任务
    /// </summary>
    public IEnumerable<RunningTaskInfo> GetRunningTasks()
    {
        var runningTasks = new List<RunningTaskInfo>();
        
        // 从进度服务获取所有进行中的任务
        var allProgress = _progressService.GetAllProgress();
        
        foreach (var progress in allProgress.Where(p => p.IsActive))
        {
            runningTasks.Add(new RunningTaskInfo
            {
                TaskId = progress.TaskId,
                TaskName = progress.TaskName,
                Progress = progress.ProgressPercentage,
                Message = progress.CurrentMessage,
                StartTime = progress.StartTime ?? DateTime.UtcNow
            });
        }
        
        return runningTasks;
    }
    
    /// <summary>
    /// 获取任务队列状态
    /// </summary>
    public Dictionary<string, QueueStatus> GetQueueStatus()
    {
        // 这里应该从 Hangfire 获取实际的队列状态
        // 暂时返回示例数据
        return new Dictionary<string, QueueStatus>
        {
            ["critical"] = new QueueStatus { QueueName = "critical", PendingCount = 0, ProcessingCount = 0 },
            ["high"] = new QueueStatus { QueueName = "high", PendingCount = 0, ProcessingCount = 0 },
            ["default"] = new QueueStatus { QueueName = "default", PendingCount = 0, ProcessingCount = 0 },
            ["low"] = new QueueStatus { QueueName = "low", PendingCount = 0, ProcessingCount = 0 },
            ["background"] = new QueueStatus { QueueName = "background", PendingCount = 0, ProcessingCount = 0 }
        };
    }
    
    /// <summary>
    /// 获取执行历史
    /// </summary>
    public IEnumerable<TaskExecutionInfo> GetExecutionHistory()
    {
        return _executionHistory.Values.OrderByDescending(h => h.StartTime);
    }
    
    /// <summary>
    /// 获取任务统计信息
    /// </summary>
    public TaskStatistics GetStatistics()
    {
        lock (_statsLock)
        {
            var history = _executionHistory.Values.ToList();
            
            return new TaskStatistics
            {
                TotalExecuted = history.Count,
                SuccessCount = history.Count(h => h.Success),
                FailedCount = history.Count(h => !h.Success),
                AverageExecutionTime = history.Any() 
                    ? TimeSpan.FromSeconds(history.Average(h => h.Duration.TotalSeconds))
                    : TimeSpan.Zero,
                LastExecutionTime = history.OrderByDescending(h => h.StartTime).FirstOrDefault()?.StartTime
            };
        }
    }
    
    #endregion
    
    
    #region 辅助方法
    
    /// <summary>
    /// 初始化定时任务配置
    /// </summary>
    private void InitializeScheduledTaskConfigs()
    {
        // 从配置文件加载定时任务
        if (_config.Tasks?.ScheduledTasks != null)
        {
            foreach (var taskConfig in _config.Tasks.ScheduledTasks)
            {
                // 验证任务类型是否存在
                if (!_taskFactory.TaskExists(taskConfig.Type))
                {
                    Log.Warning("配置中的任务类型未注册: {Type}，跳过注册", taskConfig.Type);
                    continue;
                }

                // 使用 type 作为配置的 key（与工厂的 key 一致）
                _scheduledTaskConfigs[taskConfig.Type] = taskConfig;

                // 如果任务启用且有Cron表达式，注册到Hangfire
                if (taskConfig.Enabled && !string.IsNullOrEmpty(taskConfig.CronExpression))
                {
                    RegisterScheduledTask(taskConfig);
                }
            }
        }

        Log.Information("初始化了 {Count} 个定时任务配置", _scheduledTaskConfigs.Count);
    }
    
    /// <summary>
    /// 注册定时任务到Hangfire
    /// </summary>
    private void RegisterScheduledTask(ScheduledTaskConfig config)
    {
        try
        {
            var priority = Enum.TryParse<TaskPriority>(config.Priority, out var p) ? p : TaskPriority.Normal;
            var queueName = GetQueueName(priority);

            // 使用 config.Type 作为任务 key（与工厂的 key 一致）
            // 队列通过"调用哪个带 [Queue] 特性的包装方法"来确定，而非 AddOrUpdate 的 queue 参数
            // （MemoryStorage 不支持动态 queue）。ScheduledByQueue 按 queueName 返回对应表达式。
            _recurringJobManager.AddOrUpdate<UnifiedTaskService>(
                config.Name,
                BuildScheduledExpression(queueName, config.Type),
                config.CronExpression,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });

            Log.Information("注册定时任务: {TaskName} (Type: {TaskType}, Cron: {CronExpression})",
                config.Name, config.Type, config.CronExpression);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "注册定时任务失败: {TaskName}", config.Name);
        }
    }
    
    /// <summary>
    /// 根据任务类型和优先级获取队列名称
    /// </summary>
    private string GetQueueName(ITask task)
    {
        // 识别任务使用专用队列
        if (task is IIdentificationTask)
        {
            return "identification";
        }

        // 其他任务根据优先级分配队列
        return GetQueueName(task.Priority);
    }

    /// <summary>
    /// 根据优先级获取队列名称
    /// </summary>
    private string GetQueueName(TaskPriority priority)
    {
        return priority switch
        {
            TaskPriority.Critical => "critical",
            TaskPriority.High => "high",
            TaskPriority.Normal => "default",
            TaskPriority.Low => "low",
            TaskPriority.Background => "background",
            _ => "default"
        };
    }
    
    /// <summary>
    /// 映射 Hangfire 状态到任务状态
    /// </summary>
    private TaskExecutionStatus MapHangfireState(string? state)
    {
        return state?.ToLower() switch
        {
            "enqueued" => TaskExecutionStatus.Pending,
            "processing" => TaskExecutionStatus.Running,
            "succeeded" => TaskExecutionStatus.Succeeded,
            "failed" => TaskExecutionStatus.Failed,
            "deleted" => TaskExecutionStatus.Cancelled,
            _ => TaskExecutionStatus.Unknown
        };
    }

    /// <summary>
    /// 更新执行历史
    /// </summary>
    private void UpdateExecutionHistory(string taskId, TaskResult result)
    {
        // 从ResultData中提取源路径
        string? sourcePath = null;
        if (result.ResultData?.TryGetValue("filePath", out var filePathObj) == true)
        {
            sourcePath = filePathObj?.ToString();
        }

        // 从进度服务中获取日志
        string? logEntriesJson = null;
        var progress = _progressService.GetProgress(taskId);
        if (progress?.LogEntries.Any() == true)
        {
            try
            {
                logEntriesJson = JsonSerializer.Serialize(progress.LogEntries);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "序列化任务日志失败: {TaskId}", taskId);
            }
        }

        // 从 ResultData 提取识别诊断信息（仅识别任务有）
        string? diagnosticsJson = null;
        if (result.ResultData?.TryGetValue("identificationDiagnostics", out var diagObj) == true
            && diagObj is IdentificationDiagnostics diag)
        {
            try
            {
                diagnosticsJson = JsonSerializer.Serialize(diag);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "序列化识别诊断信息失败: {TaskId}", taskId);
            }
        }

        var info = new TaskExecutionInfo
        {
            TaskId = taskId,
            TaskName = result.TaskName,
            TaskType = result.TaskType,
            StartTime = result.StartTime,
            EndTime = result.EndTime,
            Duration = result.Duration,
            Success = result.Success,
            Message = result.Message,
            ProcessedItems = result.ProcessedItems,
            FailedItems = result.FailedItems,
            SourcePath = sourcePath,
            LogEntriesJson = logEntriesJson,
            IdentificationDiagnosticsJson = diagnosticsJson,
        };

        _executionHistory[taskId] = info;

        // 限制历史记录数量
        if (_executionHistory.Count > 1000)
        {
            var oldestKey = _executionHistory.OrderBy(kvp => kvp.Value.StartTime).First().Key;
            _executionHistory.TryRemove(oldestKey, out _);
        }
    }
    
    /// <summary>
    /// 更新定时任务历史
    /// </summary>
    private void UpdateScheduledTaskHistory(string taskName, TaskResult result)
    {
        // 从进度服务中获取日志
        string? logEntriesJson = null;
        var progress = _progressService.GetProgress(result.TaskId);
        if (progress?.LogEntries.Any() == true)
        {
            try
            {
                logEntriesJson = JsonSerializer.Serialize(progress.LogEntries);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "序列化定时任务日志失败: {TaskId}", result.TaskId);
            }
        }

        var info = new TaskExecutionInfo
        {
            TaskId = result.TaskId,
            TaskName = taskName,
            TaskType = result.TaskType,
            StartTime = result.StartTime,
            EndTime = result.EndTime,
            Duration = result.Duration,
            Success = result.Success,
            Message = result.ErrorMessage ?? result.Message ?? "成功",
            ProcessedItems = result.ProcessedItems,
            FailedItems = result.FailedItems,
            LogEntriesJson = logEntriesJson
        };

        _executionHistory[info.TaskId] = info;
    }

    /// <summary>
    /// 取消正在执行或等待中的任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <returns>是否成功取消</returns>
    public bool CancelTask(string taskId)
    {
        try
        {
            // 检查是否是监控任务
            var task = _metadataStore.GetTaskAsync(taskId).GetAwaiter().GetResult();

            if (task?.TaskType == TaskType.FolderMonitor)
            {
                // 获取folderPath并调用MonitorService.Stop
                if (task.Parameters?.TryGetValue("path", out var folderPath) == true ||
                    task.Parameters?.TryGetValue("folderPath", out folderPath) == true)
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();
                    var stopped = monitorService.StopMonitoring(folderPath.ToString()!).GetAwaiter().GetResult();

                    if (stopped)
                    {
                        Log.Information("已停止文件夹监控: {Path}", folderPath);
                        // 同时清理任务元数据
                        _metadataStore.RemoveTaskAsync(taskId).GetAwaiter().GetResult();
                        return true;
                    }
                    else
                    {
                        Log.Warning("停止文件夹监控失败: {Path}", folderPath);
                        return false;
                    }
                }
            }

            // 从元数据存储获取对应的 Hangfire JobId
            var jobId = _metadataStore.GetJobIdByTaskId(taskId);
            if (string.IsNullOrEmpty(jobId))
            {
                Log.Warning("CancelTask: 无法找到任务 {TaskId} 对应的 Hangfire Job", taskId);
                return false;
            }

            // 调用 Hangfire API 删除任务（相当于取消）
            BackgroundJob.Delete(jobId);
            Log.Information("已取消任务Job: TaskId={TaskId}, JobId={JobId}", taskId, jobId);

            // 如果是父任务，同时取消所有子任务
            var childJobIds = _metadataStore.GetChildJobIds(taskId);
            if (childJobIds?.Any() == true)
            {
                Log.Information("父任务 {TaskId} 包含 {Count} 个子任务，开始批量取消",
                    taskId, childJobIds.Count);

                foreach (var childJobId in childJobIds)
                {
                    try
                    {
                        BackgroundJob.Delete(childJobId);
                        Log.Debug("已取消子任务Job: JobId={JobId}", childJobId);
                    }
                    catch (Exception childEx)
                    {
                        Log.Warning(childEx, "取消子任务Job失败: JobId={JobId}", childJobId);
                    }
                }

                Log.Information("已取消父任务 {TaskId} 及其 {Count} 个子任务",
                    taskId, childJobIds.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "取消任务失败: TaskId={TaskId}", taskId);
            return false;
        }
    }

    /// <summary>
    /// 批量取消任务（包括父任务的所有子任务）
    /// </summary>
    /// <param name="parentTaskId">父任务ID</param>
    /// <returns>成功取消的任务数量</returns>
    public int CancelTaskTree(string parentTaskId)
    {
        var canceledCount = 0;

        // 获取父任务及所有子任务
        var taskTree = _progressService.GetTaskTree(parentTaskId);
        if (taskTree == null) return 0;

        // 递归取消所有任务
        void CancelRecursive(TaskProgress task)
        {
            if (CancelTask(task.TaskId))
            {
                canceledCount++;
            }

            foreach (var child in task.ChildTasks)
            {
                CancelRecursive(child);
            }
        }

        CancelRecursive(taskTree);

        Log.Information("已取消任务树: ParentTaskId={ParentTaskId}, 取消数量={Count}", parentTaskId, canceledCount);
        return canceledCount;
    }

    #endregion

    #region 按队列分发的 Hangfire 包装方法

    // Hangfire.MemoryStorage 不支持 Enqueue<T>(queue, expr) 动态指定队列，
    // 只能通过方法上的 [Queue(...)] 特性指定。这里为 6 个优先级队列各准备一个包装方法，
    // 按 queueName 分发到对应方法让 Hangfire 读到正确的 [Queue] 特性。
    //
    // Dashboard 的 Queue 列因此会正确显示 critical/high/identification/default/low/background，
    // 而不再全部落到 default。

    // ---- ExecuteTaskAsync 的 5 个非 default 队列包装（default 用原方法 ExecuteTaskAsync）----

    [Queue("critical"), AutomaticRetry(Attempts = 0)]
    public Task ExecuteTaskAsyncCritical(string taskId, PerformContext? context) => ExecuteTaskAsync(taskId, context);

    [Queue("high"), AutomaticRetry(Attempts = 0)]
    public Task ExecuteTaskAsyncHigh(string taskId, PerformContext? context) => ExecuteTaskAsync(taskId, context);

    [Queue("identification"), AutomaticRetry(Attempts = 0)]
    public Task ExecuteTaskAsyncIdentification(string taskId, PerformContext? context) => ExecuteTaskAsync(taskId, context);

    [Queue("low"), AutomaticRetry(Attempts = 0)]
    public Task ExecuteTaskAsyncLow(string taskId, PerformContext? context) => ExecuteTaskAsync(taskId, context);

    [Queue("background"), AutomaticRetry(Attempts = 0)]
    public Task ExecuteTaskAsyncBackground(string taskId, PerformContext? context) => ExecuteTaskAsync(taskId, context);

    /// <summary>
    /// 按队列名分发 ExecuteTaskAsync 的 Hangfire 入队（SubmitTaskAsync 与父任务创建子任务都走这里）。
    /// </summary>
    private string EnqueueTaskByQueue(string queueName, string taskId) => queueName switch
    {
        "critical" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsyncCritical(taskId, null)),
        "high" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsyncHigh(taskId, null)),
        "identification" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsyncIdentification(taskId, null)),
        "low" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsyncLow(taskId, null)),
        "background" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsyncBackground(taskId, null)),
        _ => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteTaskAsync(taskId, null))
    };

    // ---- ExecuteParentTaskAsync 的 5 个非 default 队列包装 ----

    [Queue("critical"), JobDisplayName("父任务: {0}")]
    public Task ExecuteParentTaskAsyncCritical(string parentTaskId, PerformContext? context) => ExecuteParentTaskAsync(parentTaskId, context);

    [Queue("high"), JobDisplayName("父任务: {0}")]
    public Task ExecuteParentTaskAsyncHigh(string parentTaskId, PerformContext? context) => ExecuteParentTaskAsync(parentTaskId, context);

    [Queue("identification"), JobDisplayName("父任务: {0}")]
    public Task ExecuteParentTaskAsyncIdentification(string parentTaskId, PerformContext? context) => ExecuteParentTaskAsync(parentTaskId, context);

    [Queue("low"), JobDisplayName("父任务: {0}")]
    public Task ExecuteParentTaskAsyncLow(string parentTaskId, PerformContext? context) => ExecuteParentTaskAsync(parentTaskId, context);

    [Queue("background"), JobDisplayName("父任务: {0}")]
    public Task ExecuteParentTaskAsyncBackground(string parentTaskId, PerformContext? context) => ExecuteParentTaskAsync(parentTaskId, context);

    private string EnqueueParentTaskByQueue(string queueName, string parentTaskId) => queueName switch
    {
        "critical" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsyncCritical(parentTaskId, null)),
        "high" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsyncHigh(parentTaskId, null)),
        "identification" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsyncIdentification(parentTaskId, null)),
        "low" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsyncLow(parentTaskId, null)),
        "background" => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsyncBackground(parentTaskId, null)),
        _ => _backgroundJobClient.Enqueue<UnifiedTaskService>(s => s.ExecuteParentTaskAsync(parentTaskId, null))
    };

    // ---- ExecuteScheduledTaskAsync 的 6 个队列包装（含 default）----

    [Queue("critical")]
    public Task ExecuteScheduledTaskAsyncCritical(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    [Queue("high")]
    public Task ExecuteScheduledTaskAsyncHigh(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    [Queue("identification")]
    public Task ExecuteScheduledTaskAsyncIdentification(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    [Queue("default")]
    public Task ExecuteScheduledTaskAsyncDefault(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    [Queue("low")]
    public Task ExecuteScheduledTaskAsyncLow(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    [Queue("background")]
    public Task ExecuteScheduledTaskAsyncBackground(string taskKey, CancellationToken ct) => ExecuteScheduledTaskAsync(taskKey, ct);

    /// <summary>
    /// 为定时任务按队列名构造对应的包装方法表达式（用于 RecurringJobManager.AddOrUpdate）。
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<UnifiedTaskService, Task>> BuildScheduledExpression(string queueName, string taskKey) => queueName switch
    {
        "critical" => s => s.ExecuteScheduledTaskAsyncCritical(taskKey, CancellationToken.None),
        "high" => s => s.ExecuteScheduledTaskAsyncHigh(taskKey, CancellationToken.None),
        "identification" => s => s.ExecuteScheduledTaskAsyncIdentification(taskKey, CancellationToken.None),
        "low" => s => s.ExecuteScheduledTaskAsyncLow(taskKey, CancellationToken.None),
        "background" => s => s.ExecuteScheduledTaskAsyncBackground(taskKey, CancellationToken.None),
        _ => s => s.ExecuteScheduledTaskAsyncDefault(taskKey, CancellationToken.None)
    };

    #endregion

    public void Dispose()
    {
        // 清理资源
        _metadataStore?.Dispose();
    }
}

/// <summary>
/// 任务执行信息
/// </summary>
public class TaskExecutionInfo
{
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public TaskType TaskType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }

    /// <summary>
    /// 源路径（用于识别任务，支持重新识别）
    /// </summary>
    public string? SourcePath { get; set; }

    /// <summary>
    /// 日志条目JSON（序列化的日志列表）
    /// </summary>
    public string? LogEntriesJson { get; set; }

    /// <summary>
    /// 识别诊断信息 JSON（仅识别类任务有），由 <see cref="IdentificationDiagnosticsContext"/> 收集，
    /// 经 ResultData["identificationDiagnostics"] 中转后序列化进来。
    /// </summary>
    public string? IdentificationDiagnosticsJson { get; set; }

    /// <summary>
    /// 获取日志条目列表
    /// </summary>
    public List<TaskLogEntry> GetLogEntries()
    {
        if (string.IsNullOrEmpty(LogEntriesJson))
            return new List<TaskLogEntry>();

        try
        {
            return JsonSerializer.Deserialize<List<TaskLogEntry>>(LogEntriesJson) ?? new List<TaskLogEntry>();
        }
        catch
        {
            return new List<TaskLogEntry>();
        }
    }

    /// <summary>
    /// 获取识别诊断信息（仅识别类任务返回非 null）。
    /// </summary>
    public Models.Tasks.Diagnostics.IdentificationDiagnostics? GetIdentificationDiagnostics()
    {
        if (string.IsNullOrEmpty(IdentificationDiagnosticsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<Models.Tasks.Diagnostics.IdentificationDiagnostics>(IdentificationDiagnosticsJson);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 任务统计信息
/// </summary>
public class TaskStatistics
{
    public int TotalExecuted { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public DateTime? LastExecutionTime { get; set; }
}

/// <summary>
/// 运行中任务信息
/// </summary>
public class RunningTaskInfo
{
    public string TaskId { get; set; } = null!;
    public string TaskName { get; set; } = null!;
    public double Progress { get; set; }
    public string? Message { get; set; }
    public DateTime StartTime { get; set; }
}

/// <summary>
/// 队列状态
/// </summary>
public class QueueStatus
{
    public string QueueName { get; set; } = null!;
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
}


