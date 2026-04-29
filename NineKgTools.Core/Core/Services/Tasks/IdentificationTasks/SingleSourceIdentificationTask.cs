using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.IdentificationTasks;

/// <summary>
/// 单媒体源识别任务 - 处理单个媒体源（文件或文件夹）
/// </summary>
public class SingleSourceIdentificationTask : IIdentificationTask
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _filePath;
    private readonly IdentificationOptions? _options;

    public string TaskId { get; }
    public string TaskName { get; }
    public TaskType TaskType => TaskType.SingleSourceIdentification;
    public string? Description => $"识别文件: {Path.GetFileName(_filePath)}";
    public TaskPriority Priority { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }

    // 父子任务关系（来自 ITask 接口）
    public string? ParentTaskId { get; set; }
    public List<string> ChildTaskIds { get; } = new();
    public string? HangfireBatchId { get; set; }

    public string TargetPath => _filePath;
    public IdentificationOptions? Options => _options;
    public bool IsBatch => false;

    public SingleSourceIdentificationTask(
        IServiceScopeFactory serviceScopeFactory,
        string filePath,
        IdentificationOptions? options = null,
        TaskPriority priority = TaskPriority.Normal)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _filePath = filePath;
        _options = options;
        Priority = priority;
        TaskId = Guid.NewGuid().ToString();
        TaskName = $"识别: {Path.GetFileName(filePath)}";

        Parameters = new Dictionary<string, object>
        {
            ["path"] = filePath
        };

        if (options != null)
        {
            Parameters["options"] = options;
        }
    }
    
    public async Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // 开启识别诊断作用域：从这里起所有 WebsiteService / IWebsite 实现的调用都会向此 diagnostics 上报
        var diagnostics = new IdentificationDiagnostics
        {
            SourcePath = _filePath,
            PossibleTopCategory = TryGetPossibleTopCategory(),
            StartTime = startTime,
            Reporter = progressReporter, // Search 类等深处代码可通过 IdentificationDiagnosticsContext.DebugAsync 写日志
        };
        using var diagScope = IdentificationDiagnosticsContext.BeginScope(diagnostics);
        AttachDiagnosticsToProgress(diagnostics);

        try
        {
            await progressReporter.StartAsync($"开始识别: {Path.GetFileName(_filePath)}", 1);

            var media = await IdentifyAsync(progressReporter, cancellationToken);
            diagnostics.EndTime = DateTime.UtcNow;

            if (media != null)
            {
                await progressReporter.CompleteAsync($"识别成功: {media.Title}", 1, 0);

                var successResult = TaskResult.CreateSuccess(TaskId, TaskName, $"成功识别: {media.Title}", 1);
                successResult.StartTime = startTime;
                successResult.ResultData = new Dictionary<string, object>
                {
                    ["mediaId"] = media.Id,
                    ["title"] = media.Title,
                    ["mediaType"] = media.GetType().Name,
                    ["filePath"] = _filePath,
                    ["identificationDiagnostics"] = diagnostics,
                };
                return successResult;
            }
            else
            {
                if (diagnostics.OverallFailureReason == null)
                {
                    diagnostics.OverallFailureReason = "未能获取媒体信息";
                }
                await progressReporter.FailAsync("识别失败: 未能获取媒体信息");
                var failureResult = TaskResult.CreateFailure(TaskId, TaskName, "识别失败");
                failureResult.StartTime = startTime;
                failureResult.FailedItems = 1;
                failureResult.ResultData = new Dictionary<string, object>
                {
                    ["filePath"] = _filePath,
                    ["identificationDiagnostics"] = diagnostics,
                };
                return failureResult;
            }
        }
        catch (Exception ex)
        {
            diagnostics.EndTime = DateTime.UtcNow;
            diagnostics.OverallFailureReason = ex is OperationCanceledException ? "任务被取消" : $"识别异常: {ex.Message}";
            Log.Error(ex, "文件识别任务执行失败: {Path}", _filePath);
            await progressReporter.FailAsync($"识别异常: {ex.Message}", ex);

            var errorResult = TaskResult.CreateFailure(TaskId, TaskName, ex.Message, ex);
            errorResult.StartTime = startTime;
            errorResult.FailedItems = 1;
            errorResult.ResultData = new Dictionary<string, object>
            {
                ["filePath"] = _filePath,
                ["identificationDiagnostics"] = diagnostics,
            };
            return errorResult;
        }
    }

    /// <summary>
    /// 用 MediaSourceFactory 解析一次文件名以拿到 PossibleTopCategory（仅用于诊断展示）。
    /// 失败时退化为 "Unknown"，不影响真实识别流程。
    /// </summary>
    private string TryGetPossibleTopCategory()
    {
        try
        {
            var src = MediaSourceFactory.Create(_filePath);
            return src.PossibleTopCategory.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// 把诊断对象通过 TaskProgressService 挂到 TaskProgress 上，让运行中查询任务详情时能拿到实时累积的诊断。
    /// </summary>
    private void AttachDiagnosticsToProgress(IdentificationDiagnostics diagnostics)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var progressService = scope.ServiceProvider.GetService<TaskProgressService>();
            progressService?.AttachDiagnostics(TaskId, diagnostics);
        }
        catch (Exception ex)
        {
            // 挂载失败不影响识别本身，只让运行中详情看不到实时诊断
            Log.Warning(ex, "挂载识别诊断到 TaskProgress 失败: {TaskId}", TaskId);
        }
    }

    public async Task<MediaBase?> IdentifyAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        // 创建新的作用域并解析FilesService
        using var scope = _serviceScopeFactory.CreateScope();
        var filesService = scope.ServiceProvider.GetRequiredService<FilesService>();

        await progressReporter.DebugAsync("开始解析文件名...", _filePath);

        // 直接传递progressReporter和cancellationToken给FilesService
        var media = await filesService.GetMediaByPath(_filePath, _options, progressReporter, cancellationToken);

        if (media != null)
        {
            if (_options?.AutoAddToDatabase == true)
            {
                await progressReporter.PhaseAsync("正在保存到数据库", 90);
                await filesService.AddMediaToDatabase(media, cancellationToken);
                await progressReporter.SuccessAsync("已保存到数据库", null, _filePath);
                Log.Information("媒体已自动添加到数据库: {Title} ({FilePath})", media.Title, _filePath);
            }
            else
            {
                // 识别成功但不自动入库：落 Pending，等待人工在"待处理"页面确认入库
                await progressReporter.PhaseAsync("正在暂存识别结果", 90);
                await filesService.SaveIdentifiedButPendingAsync(media, cancellationToken);
                await progressReporter.SuccessAsync("已保存为待入库", null, _filePath);
                Log.Information("媒体已暂存为待入库: {Title} ({FilePath})", media.Title, _filePath);
            }
        }

        return media;
    }
    
    public Task<BatchIdentificationResult> IdentifyBatchAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        // 单文件识别不支持批量
        throw new NotSupportedException("单文件识别任务不支持批量识别");
    }
    
    public Task<List<string>> GetFilesToIdentifyAsync()
    {
        // 单文件识别只返回一个文件
        return Task.FromResult(new List<string> { _filePath });
    }
    
    public bool ValidateParameters()
    {
        if (string.IsNullOrEmpty(_filePath))
            return false;

        // 如果是手动识别模式，需要验证必要的参数
        if (_options?.Strategy == IdentificationStrategy.Manual)
        {
            return !string.IsNullOrEmpty(_options.PreferredWebsite) &&
                   (!string.IsNullOrEmpty(_options.WebsiteSpecificId) ||
                    _options.WebsiteIds?.Count > 0);
        }

        // 检查文件/目录是否存在
        if (!File.Exists(_filePath) && !Directory.Exists(_filePath))
            return false;

        // 使用统一的文件筛选逻辑（自动识别模式）
        using var scope = _serviceScopeFactory.CreateScope();
        var filesService = scope.ServiceProvider.GetRequiredService<FilesService>();
        return filesService.IsValidMediaSource(_filePath);
    }
    
    public TimeSpan? GetEstimatedDuration()
    {
        // 根据识别选项估算时间
        if (_options?.Strategy == IdentificationStrategy.Manual)
        {
            // 手动识别通常更快
            return TimeSpan.FromSeconds(5);
        }
        
        // 自动识别可能需要查询多个网站
        return TimeSpan.FromSeconds(15);
    }
}