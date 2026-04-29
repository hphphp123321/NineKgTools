namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// TaskType 枚举扩展方法
/// </summary>
public static class TaskTypeExtensions
{
    /// <summary>
    /// 获取任务类型的显示名称
    /// </summary>
    public static string GetDisplayName(this TaskType taskType) => taskType switch
    {
        // 识别任务
        TaskType.SingleSourceIdentification => "单文件识别",
        TaskType.BatchSourceIdentification => "批量文件识别",

        // 后台任务
        TaskType.FolderMonitor => "文件夹监控",

        // 定时任务
        TaskType.CacheCleanup => "缓存清理",
        TaskType.MediaCleanup => "媒体清理",
        TaskType.TagVectorSync => "标签向量同步",
        TaskType.MediaVectorSync => "媒体向量同步",

        // 其他
        TaskType.Custom => "自定义任务",
        _ => taskType.ToString()
    };

    /// <summary>
    /// 获取任务类型的分类名称
    /// </summary>
    public static string GetCategory(this TaskType taskType) => taskType switch
    {
        TaskType.SingleSourceIdentification or TaskType.BatchSourceIdentification => "识别任务",
        TaskType.FolderMonitor => "后台任务",
        TaskType.CacheCleanup or TaskType.MediaCleanup or TaskType.TagVectorSync or TaskType.MediaVectorSync => "定时任务",
        _ => "其他任务"
    };
}
