using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Tasks.Base;
using NineKgTools.Core.Services.Tasks.Interfaces;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.ScheduledTasks;

/// <summary>
/// 缓存清理任务
/// </summary>
[ScheduledTask("CacheCleanup", "缓存清理", TaskType.CacheCleanup)]
public class CacheCleanupTask : ScheduledTaskBase
{
    private readonly ImageService _imageService;
    private readonly Config _config;
    
    public override TaskType TaskType => TaskType.CacheCleanup;
    public override string TaskName => "缓存清理任务";
    public override string? TaskDescription => "清理未使用的图片缓存、更新图片哈希值并清理过期文件";
    
    public CacheCleanupTask(ImageService imageService, Config config)
    {
        _imageService = imageService;
        _config = config;
    }
    
    public override async Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var maxAgeDays = GetMaxAgeDays(parameters);
            var cachePath = _config.Cache?.Path ?? "Cache";

            int deletedFiles = 0;
            long freedSpace = 0;

            Log.Information("开始执行缓存清理任务");

            // 1. 清理未使用的图片缓存（主要功能）
            Log.Information("清理未使用的图片缓存");
            await _imageService.RemoveUnusedImgCache();

            // 2. 更新缺失的图片哈希值
            Log.Information("更新缺失的图片哈希值");
            await _imageService.UpdateMissingImageHashesAsync();

            // 3. 清理过期的缓存文件（如果指定了天数）
            if (maxAgeDays > 0 && Directory.Exists(cachePath))
            {
                var cutoffDate = DateTime.Now.AddDays(-maxAgeDays);
                Log.Information("清理 {Days} 天前的缓存文件", maxAgeDays);

                // 清理过期的图片缓存（根据配置）
                var cleanupImageCache = _config.Tasks?.CacheCleanup?.CleanupImageCache ?? true;
                if (cleanupImageCache)
                {
                    var imageResult = await CleanupExpiredImageCache(cutoffDate, cancellationToken);
                    deletedFiles += imageResult.deleted;
                    freedSpace += imageResult.size;
                }

                // 清理通用缓存目录（根据配置）
                var cleanupTempFiles = _config.Tasks?.CacheCleanup?.CleanupTempFiles ?? true;
                if (cleanupTempFiles)
                {
                    var generalResult = await CleanupGeneralCache(cachePath, cutoffDate, cancellationToken);
                    deletedFiles += generalResult.deleted;
                    freedSpace += generalResult.size;
                }
            }

            // 获取当前缓存统计
            var stats = GetCacheStatistics(cachePath);

            var message = $"缓存清理完成: ";
            if (deletedFiles > 0)
            {
                message += $"删除 {deletedFiles} 个过期文件, 释放 {FormatFileSize(freedSpace)} 空间. ";
            }
            message += $"当前缓存: {stats.fileCount} 个文件, {FormatFileSize(stats.totalSize)}";

            Log.Information(message);
            return TaskResult.CreateSuccess(TaskId, TaskName, message, deletedFiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "缓存清理任务执行失败");
            return TaskResult.CreateFailure(TaskId, TaskName, $"任务执行失败: {ex.Message}", ex);
        }
    }
    
    public override bool ValidateParameters(Dictionary<string, object>? parameters)
    {
        if (parameters == null || !parameters.Any())
        {
            return true;
        }
        
        if (parameters.TryGetValue("max_age_days", out var value))
        {
            return value is int || value is long || value is double;
        }
        
        return true;
    }
    
    private int GetMaxAgeDays(Dictionary<string, object>? parameters)
    {
        // 1. 优先使用 TaskConfig 中的配置
        var configValue = _config.Tasks?.CacheCleanup?.MaxAgeDays ?? 0;
        if (configValue > 0)
        {
            return configValue;
        }

        // 2. 向后兼容：从 parameters 获取
        if (parameters?.TryGetValue("max_age_days", out var value) == true)
        {
            if (value is int intValue)
                return intValue;
            if (value is long longValue)
                return (int)longValue;
            if (value is double doubleValue)
                return (int)doubleValue;
        }

        // 默认不清理过期文件，只清理未使用的缓存
        return 0;
    }
    
    private async Task<(int deleted, long size)> CleanupExpiredImageCache(DateTime cutoffDate, CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        long totalSize = 0;
        
        try
        {
            // 清理过期的图片缓存
            var imageCachePath = Path.Combine(_config.Cache?.Path ?? "Cache", "images");
            if (Directory.Exists(imageCachePath))
            {
                var files = Directory.GetFiles(imageCachePath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastAccessTime < cutoffDate)
                        {
                            totalSize += fileInfo.Length;
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "删除缓存文件失败: {File}", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理图片缓存时出错");
        }
        
        return (deletedCount, totalSize);
    }
    
    private async Task<(int deleted, long size)> CleanupGeneralCache(string cachePath, DateTime cutoffDate, CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        long totalSize = 0;
        
        try
        {
            // 清理临时文件
            var tempPatterns = new[] { "*.tmp", "*.temp", "*.cache", "~*" };
            
            foreach (var pattern in tempPatterns)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                var files = Directory.GetFiles(cachePath, pattern, SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            totalSize += fileInfo.Length;
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "删除临时文件失败: {File}", file);
                    }
                }
            }
            
            // 清理空目录
            await Task.Run(() => CleanupEmptyDirectories(cachePath), cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理通用缓存时出错");
        }
        
        return (deletedCount, totalSize);
    }
    
    private void CleanupEmptyDirectories(string path)
    {
        try
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                CleanupEmptyDirectories(directory);
                
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                    Log.Debug("删除空目录: {Directory}", directory);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理空目录时出错: {Path}", path);
        }
    }
    
    private (int fileCount, long totalSize) GetCacheStatistics(string cachePath)
    {
        int fileCount = 0;
        long totalSize = 0;
        
        try
        {
            if (Directory.Exists(cachePath))
            {
                var files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                fileCount = files.Length;
                
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch
                    {
                        // 忽略无法访问的文件
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取缓存统计信息时出错");
        }
        
        return (fileCount, totalSize);
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}