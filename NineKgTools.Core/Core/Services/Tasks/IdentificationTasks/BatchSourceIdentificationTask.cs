using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.IdentificationTasks;

/// <summary>
/// 批量媒体源识别任务 - 使用并发子任务处理
/// 将每个文件作为独立的 SingleSourceIdentificationTask 子任务并行处理
/// </summary>
public class BatchSourceIdentificationTask : IParentTask, IIdentificationTask
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly string _folderPath;
    private readonly IdentificationOptions? _options;
    private readonly int _maxDepth;
    private readonly string[]? _extensions;
    private readonly bool _startMonitoringAfterCompletion;
    private readonly IdentificationOptions? _monitorOptions;
    private BatchIdentificationResult? _batchResult;

    public string TaskId { get; }
    public string TaskName { get; }
    public TaskType TaskType => TaskType.BatchSourceIdentification;
    public string? Description => $"批量识别文件夹: {Path.GetFileName(_folderPath)}";
    public TaskPriority Priority { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }

    // 父子任务关系（来自 ITask 接口）
    public string? ParentTaskId { get; set; }
    public List<string> ChildTaskIds { get; } = new();
    public string? HangfireBatchId { get; set; }

    // IIdentificationTask 属性
    public string TargetPath => _folderPath;
    public IdentificationOptions? Options => _options;
    public bool IsBatch => true;

    public BatchSourceIdentificationTask(
        IServiceScopeFactory serviceScopeFactory,
        string folderPath,
        IdentificationOptions? options = null,
        int maxDepth = -1,
        string[]? extensions = null,
        TaskPriority priority = TaskPriority.Normal,
        bool startMonitoringAfterCompletion = true,
        IdentificationOptions? monitorOptions = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _folderPath = folderPath;
        _options = options;
        _maxDepth = maxDepth;
        _extensions = extensions;
        _startMonitoringAfterCompletion = startMonitoringAfterCompletion;
        _monitorOptions = monitorOptions;
        Priority = priority;
        TaskId = Guid.NewGuid().ToString();
        TaskName = $"批量识别: {Path.GetFileName(folderPath)}";

        Parameters = new Dictionary<string, object>
        {
            ["path"] = folderPath,
            ["maxDepth"] = maxDepth,
            ["startMonitoring"] = startMonitoringAfterCompletion
        };

        if (extensions != null)
        {
            Parameters["extensions"] = extensions;
        }
    }

    /// <summary>
    /// 父任务自身不执行实际识别，只是作为子任务的容器
    /// </summary>
    public async Task<TaskResult> ExecuteAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        // 父任务本身不做实际工作，只是在创建子任务前做准备
        await progressReporter.StartAsync($"开始批量识别文件夹: {Path.GetFileName(_folderPath)}");

        // 实际的识别工作由子任务完成
        // 这个方法不会被直接调用，而是通过 UnifiedTaskService 的父任务机制来处理

        await progressReporter.CompleteAsync("父任务准备完成，等待子任务执行", 0, 0);

        return TaskResult.CreateSuccess(TaskId, TaskName, "父任务容器创建成功");
    }

    #region IParentTask 接口实现

    /// <summary>
    /// 创建子任务列表 - 为每个文件创建独立的 SingleSourceIdentificationTask
    /// </summary>
    public async Task<List<ITask>> CreateChildTasksAsync()
    {
        Log.Information("开始为文件夹 {FolderPath} 创建子任务", _folderPath);

        // 获取所有待识别的文件
        var files = await GetFilesToIdentifyAsync();

        if (files.Count == 0)
        {
            Log.Warning("文件夹 {FolderPath} 中没有找到需要识别的文件", _folderPath);
            return new List<ITask>();
        }

        // 为每个文件创建独立的子任务
        var childTasks = new List<ITask>();
        foreach (var filePath in files)
        {
            var childTask = new SingleSourceIdentificationTask(
                _serviceScopeFactory,
                filePath,
                _options,
                Priority // 子任务继承父任务的优先级
            );

            // 设置父任务关系
            childTask.ParentTaskId = TaskId;

            childTasks.Add(childTask);
        }

        Log.Information("为文件夹 {FolderPath} 创建了 {Count} 个子任务", _folderPath, childTasks.Count);

        // 初始化批量结果对象
        _batchResult = new BatchIdentificationResult
        {
            TaskId = TaskId,
            TaskName = TaskName
        };

        return childTasks;
    }

    /// <summary>
    /// 单个子任务完成时的回调
    /// </summary>
    public async Task OnChildTaskCompletedAsync(string childTaskId, TaskResult result)
    {
        if (_batchResult == null) return;

        // 实时更新批量结果
        if (result.Success)
        {
            // 如果子任务成功，记录到 IdentifiedMedias
            if (result.ResultData != null &&
                result.ResultData.TryGetValue("mediaId", out var mediaIdObj) &&
                result.ResultData.TryGetValue("title", out var titleObj) &&
                result.ResultData.TryGetValue("mediaType", out var mediaTypeObj))
            {
                var filePath = result.ResultData.ContainsKey("filePath")
                    ? result.ResultData["filePath"]?.ToString()
                    : "Unknown";

                _batchResult.IdentifiedMedias.Add(new IdentifiedMedia
                {
                    FilePath = filePath ?? "Unknown",
                    MediaId = mediaIdObj switch
                    {
                        int id => id,
                        string str when int.TryParse(str, out var id) => id,
                        _ => 0
                    },
                    Title = titleObj?.ToString() ?? "Unknown",
                    MediaType = mediaTypeObj?.ToString() ?? "Unknown"
                });
            }
        }
        else
        {
            // 如果子任务失败，记录到 FailedFiles
            var filePath = result.ResultData?.ContainsKey("filePath") == true
                ? result.ResultData["filePath"]?.ToString()
                : "Unknown";

            _batchResult.FailedFiles.Add(new FailedIdentification
            {
                FilePath = filePath ?? "Unknown",
                Reason = result.Message ?? "识别失败",
                Exception = result.Exception
            });
        }

        Log.Debug("子任务 {ChildTaskId} 完成，状态: {Status}", childTaskId, result.Success ? "成功" : "失败");

        await Task.CompletedTask;
    }

    /// <summary>
    /// 所有子任务完成后的回调 - 汇总结果并启动文件夹监控
    /// </summary>
    public async Task OnAllChildTasksCompletedAsync(List<TaskResult> childResults)
    {
        if (_batchResult == null)
        {
            _batchResult = new BatchIdentificationResult
            {
                TaskId = TaskId,
                TaskName = TaskName
            };
        }

        var successCount = childResults.Count(r => r.Success);
        var failedCount = childResults.Count - successCount;
        var totalCount = childResults.Count;

        _batchResult.ProcessedItems = totalCount;
        _batchResult.FailedItems = failedCount;
        _batchResult.Success = failedCount == 0;
        _batchResult.Message = $"批量识别完成: 成功 {successCount}/{totalCount} 个文件";

        if (failedCount > 0)
        {
            _batchResult.Message += $", 失败 {failedCount} 个";
        }

        Log.Information("文件夹批量识别任务完成: {FolderPath}, 成功: {Success}/{Total}, 失败: {Failed}",
            _folderPath, successCount, totalCount, failedCount);

        // 如果需要启动监控，创建文件夹监控任务（部分文件识别失败不应阻止监控）
        if (_startMonitoringAfterCompletion)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();
                var taskService = scope.ServiceProvider.GetRequiredService<UnifiedTaskService>();

                // 检查是否已在监控
                if (!monitorService.IsMonitoring(_folderPath))
                {
                    // 创建FolderMonitorTask
                    var monitorTask = new MonitorTasks.FolderMonitorTask(
                        _serviceScopeFactory,
                        _folderPath,
                        _monitorOptions ?? _options,
                        TaskPriority.Low  // 监控任务优先级较低
                    );

                    await taskService.SubmitTaskAsync(monitorTask);
                    Log.Information("批量识别完成，已提交文件夹监控任务: {Path}", _folderPath);
                }
                else
                {
                    Log.Information("文件夹已在监控中，跳过创建新监控任务: {Path}", _folderPath);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "启动文件夹监控任务失败: {Path}", _folderPath);
            }
        }

        await Task.CompletedTask;
    }

    #endregion

    #region IIdentificationTask 接口实现

    /// <summary>
    /// 单文件识别（不支持）
    /// </summary>
    public Task<MediaBase?> IdentifyAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("文件夹批量识别任务不支持单文件识别，请使用子任务模式");
    }

    /// <summary>
    /// 批量识别 - 返回汇总结果
    /// </summary>
    public Task<BatchIdentificationResult> IdentifyBatchAsync(IProgressReporter progressReporter, CancellationToken cancellationToken = default)
    {
        // 父任务不直接执行批量识别，而是通过子任务
        if (_batchResult != null)
        {
            return Task.FromResult(_batchResult);
        }

        return Task.FromResult(new BatchIdentificationResult
        {
            TaskId = TaskId,
            TaskName = TaskName,
            Success = false,
            Message = "批量识别由子任务完成"
        });
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取待识别的文件列表
    /// </summary>
    public async Task<List<string>> GetFilesToIdentifyAsync()
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(_folderPath))
            {
                Log.Warning("文件夹不存在: {Path}", _folderPath);
                return new List<string>();
            }

            using var scope = _serviceScopeFactory.CreateScope();
            var filesService = scope.ServiceProvider.GetRequiredService<FilesService>();

            var files = new List<string>();

            // 使用深度控制扫描
            ScanDirectoryWithDepth(_folderPath, 0, _maxDepth, filesService, files);

            Log.Information("在文件夹 {Path} 中找到 {Count} 个待识别文件（扫描深度: {MaxDepth}）",
                _folderPath, files.Count, _maxDepth == -1 ? "无限" : _maxDepth.ToString());
            return files;
        });
    }

    /// <summary>
    /// 递归扫描目录，支持深度控制
    /// </summary>
    /// <param name="currentPath">当前扫描路径</param>
    /// <param name="currentDepth">当前深度（0为起始目录）</param>
    /// <param name="maxDepth">最大深度（-1为无限，0为只扫描当前目录，1为扫描一级子目录）</param>
    /// <param name="filesService">文件服务</param>
    /// <param name="results">结果列表</param>
    private void ScanDirectoryWithDepth(
        string currentPath,
        int currentDepth,
        int maxDepth,
        FilesService filesService,
        List<string> results)
    {
        try
        {
            // 扫描当前目录的文件
            var allFiles = Directory.GetFiles(currentPath, "*.*", SearchOption.TopDirectoryOnly);
            foreach (var file in allFiles)
            {
                if (filesService.IsValidMediaSource(file))
                {
                    results.Add(file);
                }
            }

            // 检查当前目录下的文件夹（可能是媒体源）
            var directories = Directory.GetDirectories(currentPath, "*", SearchOption.TopDirectoryOnly);
            foreach (var dir in directories)
            {
                // 检查文件夹本身是否是媒体源（如解压后的文件夹）
                if (IsValidMediaSource(dir))
                {
                    results.Add(dir);
                }
            }

            // 检查是否需要继续递归扫描子目录
            // maxDepth == -1：无限递归
            // maxDepth == 0：只扫描当前目录，不递归
            // maxDepth > 0：递归到指定深度
            if (maxDepth == -1 || currentDepth < maxDepth)
            {
                foreach (var dir in directories)
                {
                    // 递归扫描子目录
                    ScanDirectoryWithDepth(dir, currentDepth + 1, maxDepth, filesService, results);
                }
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning("无权访问目录: {Path}", currentPath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "扫描目录时出错: {Path}", currentPath);
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
        // 估算：每个文件约15秒（实际会并发处理，但这里返回理论总时间）
        var searchOption = _maxDepth == 0 ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories;
        var fileCount = Directory.Exists(_folderPath) ?
            Directory.GetFiles(_folderPath, "*.*", searchOption).Length : 0;

        // 并发情况下，预估时间会更短
        return TimeSpan.FromSeconds(fileCount * 15 / 5); // 假设5个并发
    }

    public Task OnBeforeExecuteAsync(CancellationToken cancellationToken = default)
    {
        // 父任务执行前的准备工作（如果需要）
        return Task.CompletedTask;
    }

    public Task OnAfterExecuteAsync(TaskResult result, CancellationToken cancellationToken = default)
    {
        // 父任务执行后的清理工作
        // 注意：文件夹监控已在 OnAllChildTasksCompletedAsync 中启动
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查文件夹是否可能是媒体源
    /// </summary>
    private bool IsValidMediaSource(string dirPath)
    {
        var dirName = Path.GetFileName(dirPath);

        // 检查是否包含典型的媒体标识符（如RJ号等）
        if (dirName.Contains("RJ", StringComparison.OrdinalIgnoreCase) ||
            dirName.Contains("VJ", StringComparison.OrdinalIgnoreCase) ||
            dirName.Contains("BJ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 检查文件夹内是否包含媒体文件
        try
        {
            var hasMediaFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly)
                .Any(f => IsMediaFileExtension(Path.GetExtension(f)));
            return hasMediaFiles;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 检查是否为媒体文件扩展名
    /// </summary>
    private bool IsMediaFileExtension(string extension)
    {
        return TopCategoryExtensions.Extensions
            .Where(kvp => kvp.Key != TopCategory.Unknown)
            .SelectMany(kvp => kvp.Value)
            .Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
