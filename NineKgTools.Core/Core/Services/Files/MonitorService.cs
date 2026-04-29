using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using Serilog;

namespace NineKgTools.Core.Services.Files;

/// <summary>
/// 文件夹监控服务 - 专门管理所有文件夹监控任务
/// </summary>
public class MonitorService
{
    private readonly ConcurrentDictionary<string, MonitorTaskContext> _activeMonitors;
    private readonly TaskProgressService _progressService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly FileStabilityChecker _stabilityChecker = new();

    public MonitorService(TaskProgressService progressService, IServiceScopeFactory serviceScopeFactory)
    {
        _progressService = progressService;
        _serviceScopeFactory = serviceScopeFactory;
        _activeMonitors = new ConcurrentDictionary<string, MonitorTaskContext>(StringComparer.OrdinalIgnoreCase);

        Log.Information("MonitorService 已初始化");
    }

    /// <summary>
    /// 启动文件夹监控
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="taskId">任务ID</param>
    /// <param name="options">识别选项</param>
    /// <returns>是否成功启动</returns>
    public bool StartMonitoring(string folderPath, string taskId, IdentificationOptions? options)
    {
        try
        {
            // 标准化路径
            folderPath = Path.GetFullPath(folderPath);

            // 检查是否已在监控
            if (_activeMonitors.ContainsKey(folderPath))
            {
                Log.Warning("文件夹已在监控中: {FolderPath}", folderPath);
                return false;
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(folderPath))
            {
                Log.Warning("文件夹不存在: {FolderPath}", folderPath);
                return false;
            }

            // 创建监控上下文
            var context = new MonitorTaskContext
            {
                FolderPath = folderPath,
                TaskId = taskId,
                Options = options,
                StartTime = DateTime.UtcNow
            };

            // 创建FolderMonitor实例
            var monitor = new FolderMonitor(
                (sender, args) => OnFileCreated(args.FullPath, context),
                (sender, args) => OnFileDeleted(args.FullPath, context)
            );

            context.Monitor = monitor;

            // 启动监控（非阻塞）
            Task.Run(() => monitor.MonitorFolder(folderPath));

            // 添加到活动监控字典
            _activeMonitors[folderPath] = context;

            Log.Information("文件夹监控已启动: {FolderPath}, TaskId: {TaskId}", folderPath, taskId);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动文件夹监控失败: {FolderPath}", folderPath);
            return false;
        }
    }

    /// <summary>
    /// 停止文件夹监控
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <returns>是否成功停止</returns>
    public async Task<bool> StopMonitoring(string folderPath)
    {
        try
        {
            // 标准化路径
            folderPath = Path.GetFullPath(folderPath);

            if (!_activeMonitors.TryRemove(folderPath, out var context))
            {
                Log.Warning("文件夹不在监控中: {FolderPath}", folderPath);
                return false;
            }

            // 取消进行中的稳定性检测
            context.Cts.Cancel();

            // 停止FolderMonitor
            context.Monitor?.StopMonitorFolder(folderPath);

            // 更新任务进度为已取消
            var reporter = _progressService.CreateProgressReporter(context.TaskId, context.TaskId, taskType: TaskType.FolderMonitor);
            if (reporter != null)
            {
                await reporter.CompleteAsync($"监控已停止（已处理 {context.ProcessedCount} 个文件）",
                    context.ProcessedCount, context.FailedCount);
            }

            Log.Information("文件夹监控已停止: {FolderPath}, 处理: {Processed}, 失败: {Failed}",
                folderPath, context.ProcessedCount, context.FailedCount);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "停止文件夹监控失败: {FolderPath}", folderPath);
            return false;
        }
    }

    /// <summary>
    /// 检查文件夹是否在监控中
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <returns>是否在监控中</returns>
    public bool IsMonitoring(string folderPath)
    {
        folderPath = Path.GetFullPath(folderPath);
        return _activeMonitors.ContainsKey(folderPath);
    }

    /// <summary>
    /// 获取所有监控中的文件夹列表
    /// </summary>
    /// <returns>文件夹路径列表</returns>
    public IEnumerable<string> GetMonitoringFolders()
    {
        return _activeMonitors.Keys.ToList();
    }

    /// <summary>
    /// 获取指定路径的监控统计信息
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <returns>监控统计信息，如果未找到则返回null</returns>
    public MonitoringStats? GetMonitoringStats(string folderPath)
    {
        folderPath = Path.GetFullPath(folderPath);
        if (_activeMonitors.TryGetValue(folderPath, out var context))
        {
            return new MonitoringStats
            {
                ProcessedCount = context.ProcessedCount,
                FailedCount = context.FailedCount,
                LastActivityTime = context.LastActivityTime,
                StartTime = context.StartTime
            };
        }
        return null;
    }

    /// <summary>
    /// 获取所有监控任务的详细信息
    /// </summary>
    /// <returns>所有监控任务信息列表</returns>
    public IEnumerable<MonitoringTaskInfo> GetAllMonitoringTasks()
    {
        return _activeMonitors.Values.Select(ctx => new MonitoringTaskInfo
        {
            FolderPath = ctx.FolderPath,
            TaskId = ctx.TaskId,
            StartTime = ctx.StartTime,
            ProcessedCount = ctx.ProcessedCount,
            FailedCount = ctx.FailedCount
        }).ToList();
    }

    /// <summary>
    /// 停止所有监控
    /// </summary>
    public void StopAllMonitoring()
    {
        Log.Information("正在停止所有文件夹监控，当前监控数量: {Count}", _activeMonitors.Count);

        foreach (var kvp in _activeMonitors.ToList())
        {
            try
            {
                kvp.Value.Cts.Cancel();
                kvp.Value.Monitor?.StopMonitorFolder(kvp.Key);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "停止监控时发生错误: {FolderPath}", kvp.Key);
            }
        }

        _activeMonitors.Clear();

        Log.Information("所有文件夹监控已停止");
    }

    /// <summary>
    /// 文件创建事件处理
    /// </summary>
    private async void OnFileCreated(string filePath, MonitorTaskContext context)
    {
        try
        {
            Log.Debug("检测到新文件/文件夹: {FilePath}，等待写入完成...", filePath);

            // 等待文件/文件夹完全写入
            var isStable = await _stabilityChecker.WaitForStabilityAsync(filePath, context.Cts.Token);
            if (!isStable)
            {
                Log.Debug("文件/文件夹未能稳定或重复事件，跳过: {FilePath}", filePath);
                return;
            }

            Log.Debug("文件/文件夹已稳定: {FilePath}", filePath);

            using var scope = _serviceScopeFactory.CreateScope();
            var filesService = scope.ServiceProvider.GetRequiredService<FilesService>();

            // 验证文件是否有效
            if (!filesService.IsValidMediaSource(filePath))
            {
                Log.Debug("文件不符合媒体源要求，跳过: {FilePath}", filePath);
                return;
            }

            Log.Information("文件夹监控检测到新媒体源: {FilePath}", filePath);

            // 提交识别任务
            var taskId = await filesService.IdentifySingleMedia(filePath, context.Options);

            // 更新统计（线程安全）
            Interlocked.Increment(ref context._processedCount);
            context.LastActivityTime = DateTime.UtcNow;

            // 更新任务进度
            var reporter = _progressService.CreateProgressReporter(context.TaskId, context.TaskId, taskType: TaskType.FolderMonitor);
            if (reporter != null)
            {
                await reporter.InfoAsync(
                    $"监控中 - 已处理 {context.ProcessedCount} 个文件",
                    0,
                    filePath
                );
            }

            Log.Debug("文件识别任务已提交: {FilePath}, TaskId: {TaskId}", filePath, taskId);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("文件处理被取消: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref context._failedCount);
            Log.Error(ex, "处理新文件时发生错误: {FilePath}", filePath);

            // 更新失败计数
            var reporter = _progressService.CreateProgressReporter(context.TaskId, context.TaskId, taskType: TaskType.FolderMonitor);
            if (reporter != null)
            {
                await reporter.InfoAsync(
                    $"监控中 - 已处理 {context.ProcessedCount} 个文件（失败 {context.FailedCount}）",
                    0,
                    filePath
                );
            }
        }
    }

    /// <summary>
    /// 文件删除事件处理
    /// </summary>
    private async void OnFileDeleted(string filePath, MonitorTaskContext context)
    {
        try
        {
            Log.Information("文件夹监控检测到文件删除: {FilePath}", filePath);

            using var scope = _serviceScopeFactory.CreateScope();
            var filesService = scope.ServiceProvider.GetRequiredService<FilesService>();

            // 从数据库移除媒体
            await filesService.RemoveMediaByPath(filePath, default);

            Log.Debug("媒体已从数据库移除: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理文件删除时发生错误: {FilePath}", filePath);
        }
    }
}

/// <summary>
/// 监控任务上下文
/// </summary>
internal class MonitorTaskContext
{
    /// <summary>
    /// 文件夹路径
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// 任务ID
    /// </summary>
    public required string TaskId { get; set; }

    /// <summary>
    /// 识别选项
    /// </summary>
    public IdentificationOptions? Options { get; set; }

    /// <summary>
    /// FolderMonitor实例
    /// </summary>
    public FolderMonitor? Monitor { get; set; }

    /// <summary>
    /// 监控开始时间
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// 用于取消进行中的稳定性检测
    /// </summary>
    public CancellationTokenSource Cts { get; set; } = new();

    // 线程安全的计数字段
    internal int _processedCount;
    internal int _failedCount;

    /// <summary>
    /// 已处理文件数量
    /// </summary>
    public int ProcessedCount => _processedCount;

    /// <summary>
    /// 失败文件数量
    /// </summary>
    public int FailedCount => _failedCount;

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime? LastActivityTime { get; set; }
}
