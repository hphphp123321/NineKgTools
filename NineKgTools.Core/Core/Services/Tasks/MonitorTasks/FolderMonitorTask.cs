using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.MonitorTasks;

/// <summary>
/// 文件夹监控任务 - 后台服务任务，持续监控文件夹变化
/// </summary>
public class FolderMonitorTask : IBackgroundTask
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _folderPath;
    private readonly IdentificationOptions? _options;

    public string TaskId { get; }
    public string TaskName { get; }
    public TaskType TaskType => TaskType.FolderMonitor;
    public string? Description => $"监控文件夹: {Path.GetFileName(_folderPath)}";
    public TaskPriority Priority { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? ParentTaskId { get; set; }
    public List<string> ChildTaskIds { get; } = new();
    public string? HangfireBatchId { get; set; }

    // IBackgroundTask properties
    public bool IsRunning { get; private set; }
    public DateTime? StartedAt { get; private set; }

    public FolderMonitorTask(
        IServiceScopeFactory serviceScopeFactory,
        string folderPath,
        IdentificationOptions? options = null,
        TaskPriority priority = TaskPriority.Low)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _folderPath = folderPath;
        _options = options;
        Priority = priority;
        TaskId = Guid.NewGuid().ToString();
        TaskName = $"监控文件夹: {Path.GetFileName(folderPath)}";

        Parameters = new Dictionary<string, object>
        {
            ["path"] = folderPath,
            ["folderPath"] = folderPath,
            ["type"] = "FolderMonitor"
        };

        if (options != null)
        {
            Parameters["options"] = options;
        }
    }

    /// <summary>
    /// 执行监控任务（立即返回，非阻塞）
    /// </summary>
    public async Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Information("开始执行文件夹监控任务: {FolderPath} (TaskId: {TaskId})",
                _folderPath, TaskId);

            using var scope = _serviceScopeFactory.CreateScope();
            var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();

            // 检查是否已在监控
            if (monitorService.IsMonitoring(_folderPath))
            {
                Log.Warning("文件夹已在监控中: {FolderPath}", _folderPath);
                await progressReporter.FailAsync("文件夹已在监控中");
                return TaskResult.CreateFailure(TaskId, TaskName, "文件夹已在监控中");
            }

            // 通知开始
            await progressReporter.StartAsync("正在启动文件夹监控");

            // 启动监控（非阻塞）
            var success = monitorService.StartMonitoring(_folderPath, TaskId, _options);

            if (success)
            {
                // 标记任务为运行中
                IsRunning = true;
                StartedAt = DateTime.UtcNow;

                // 监控成功启动，任务立即完成返回
                // 监控在后台持续运行，通过MonitorService管理
                await progressReporter.CompleteAsync("文件夹监控已启动，正在后台运行", 0, 0);

                Log.Information("文件夹监控任务已启动: {FolderPath} (TaskId: {TaskId})",
                    _folderPath, TaskId);

                return TaskResult.CreateSuccess(TaskId, TaskName, "监控已启动，正在后台运行");
            }
            else
            {
                await progressReporter.FailAsync("启动文件夹监控失败");

                Log.Error("文件夹监控任务启动失败: {FolderPath} (TaskId: {TaskId})",
                    _folderPath, TaskId);

                return TaskResult.CreateFailure(TaskId, TaskName, "启动监控失败");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "文件夹监控任务执行失败: {FolderPath} (TaskId: {TaskId})",
                _folderPath, TaskId);
            await progressReporter.FailAsync($"执行失败: {ex.Message}", ex);
            return TaskResult.CreateFailure(TaskId, TaskName, $"执行失败: {ex.Message}");
        }
    }

    public bool ValidateParameters()
    {
        if (string.IsNullOrEmpty(_folderPath))
            return false;

        return Directory.Exists(_folderPath);
    }

    public TimeSpan? GetEstimatedDuration()
    {
        // 监控任务立即返回，估算执行时间很短
        return TimeSpan.FromSeconds(1);
    }

    public Task OnBeforeExecuteAsync(CancellationToken cancellationToken = default)
    {
        // 监控任务无需前置准备
        return Task.CompletedTask;
    }

    public Task OnAfterExecuteAsync(TaskResult result, CancellationToken cancellationToken = default)
    {
        // 监控任务的清理由MonitorService负责
        return Task.CompletedTask;
    }

    /// <summary>
    /// 获取后台任务的统计信息
    /// </summary>
    public BackgroundTaskStats GetStatistics()
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();
            var stats = monitorService.GetMonitoringStats(_folderPath);

            return new BackgroundTaskStats
            {
                TotalProcessed = stats?.ProcessedCount ?? 0,
                TotalFailed = stats?.FailedCount ?? 0,
                LastActivityTime = stats?.LastActivityTime,
                StatusMessage = $"监控中 - 已处理 {stats?.ProcessedCount ?? 0} 个文件"
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取监控任务统计信息失败: {FolderPath}", _folderPath);
            return new BackgroundTaskStats
            {
                StatusMessage = "无法获取统计信息"
            };
        }
    }

    /// <summary>
    /// 停止后台任务
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();
            await monitorService.StopMonitoring(_folderPath);

            IsRunning = false;
            Log.Information("文件夹监控已停止: {FolderPath} (TaskId: {TaskId})", _folderPath, TaskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "停止文件夹监控失败: {FolderPath}", _folderPath);
            throw;
        }
    }
}
