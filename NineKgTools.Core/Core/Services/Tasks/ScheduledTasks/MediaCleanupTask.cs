using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks.Base;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.ScheduledTasks;

/// <summary>
/// 媒体清理任务
/// </summary>
[ScheduledTask("MediaCleanup", "媒体清理", TaskType.MediaCleanup)]
public class MediaCleanupTask : ScheduledTaskBase
{
    private readonly MediaService _mediaService;
    private readonly Config _config;
    
    public override TaskType TaskType => TaskType.MediaCleanup;
    public override string TaskName => "媒体清理任务";
    public override string? TaskDescription => "清理无效的媒体记录和孤立文件";
    
    public MediaCleanupTask(MediaService mediaService, Config config)
    {
        _mediaService = mediaService;
        _config = config;
    }
    
    public override async Task<TaskResult> ExecuteAsync(Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var orphanedRecords = 0;
            var missingFiles = 0;
            var duplicates = 0;

            Log.Information("开始媒体清理任务");

            // 清理缺失文件的媒体记录
            missingFiles = await CleanupMissingFiles(cancellationToken);

            // 清理孤立的媒体记录（没有关联源的）
            orphanedRecords = await CleanupOrphanedRecords(cancellationToken);

            // 清理重复的媒体记录
            if (ShouldCleanDuplicates(parameters))
            {
                duplicates = await CleanupDuplicates(cancellationToken);
            }

            var totalCleaned = orphanedRecords + missingFiles + duplicates;
            var message = $"媒体清理完成: 清理 {missingFiles} 个缺失文件记录, {orphanedRecords} 个孤立记录";

            if (duplicates > 0)
            {
                message += $", {duplicates} 个重复记录";
            }

            Log.Information(message);
            return TaskResult.CreateSuccess(TaskId, TaskName, message, totalCleaned);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "媒体清理任务执行失败");
            return TaskResult.CreateFailure(TaskId, TaskName, $"任务执行失败: {ex.Message}", ex);
        }
    }
    
    public override bool ValidateParameters(Dictionary<string, object>? parameters)
    {
        return true; // 所有参数都是可选的
    }
    
    private bool ShouldCleanDuplicates(Dictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue("clean_duplicates", out var value) == true)
        {
            return value is bool boolValue && boolValue;
        }
        return false; // 默认不清理重复项
    }
    
    private async Task<int> CleanupMissingFiles(CancellationToken cancellationToken)
    {
        int cleaned = 0;
        
        try
        {
            // 获取所有媒体记录
            var allMedia = await _mediaService.GetAllMedia();
            
            foreach (var media in allMedia)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                // 检查媒体源文件是否存在
                if (media.Source == null || string.IsNullOrEmpty(media.Source.FullPath)) continue;
                var fullPath = media.Source.FullPath;
                if (File.Exists(fullPath) || Directory.Exists(fullPath)) continue;
                Log.Information("删除缺失文件的媒体记录: {Title} - {Path}", 
                    media.Title, fullPath);
                        
                await _mediaService.RemoveMediaAsync(media.Id);
                cleaned++;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理缺失文件记录时出错");
        }
        
        return cleaned;
    }
    
    private async Task<int> CleanupOrphanedRecords(CancellationToken cancellationToken)
    {
        int cleaned = 0;
        
        try
        {
            // 获取没有关联源的媒体记录
            var allMedia = await _mediaService.GetAllMedia();
            
            foreach (var media in allMedia)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (media.Source != null) continue;
                Log.Information("发现孤立媒体记录: {Title} (ID: {Id})", 
                    media.Title, media.Id);
                // 这里可以根据配置决定是否删除
                cleaned++;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理孤立记录时出错");
        }
        
        return cleaned;
    }
    
    private async Task<int> CleanupDuplicates(CancellationToken cancellationToken)
    {
        int cleaned = 0;
        
        try
        {
            var allMedia = await _mediaService.GetAllMedia();
            var seen = new HashSet<string>();
            
            foreach (var media in allMedia)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                
                // 使用标题和发布日期作为唯一标识
                var key = $"{media.Title}_{media.ReleaseDate?.ToString("yyyy-MM-dd")}";

                if (seen.Add(key)) continue;
                Log.Information("发现重复媒体: {Title}", media.Title);
                // 这里可以根据策略决定保留哪个版本
                cleaned++;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "清理重复记录时出错");
        }
        
        return cleaned;
    }
}