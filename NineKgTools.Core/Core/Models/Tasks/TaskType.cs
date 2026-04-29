namespace NineKgTools.Core.Models.Tasks;

/// <summary>
/// 任务类型枚举
/// </summary>
public enum TaskType
{
    // 识别任务
    /// <summary>
    /// 单文件识别
    /// </summary>
    SingleSourceIdentification,

    /// <summary>
    /// 批量文件识别（文件夹）
    /// </summary>
    BatchSourceIdentification,

    // 后台任务
    /// <summary>
    /// 文件夹监控
    /// </summary>
    FolderMonitor,

    // 定时任务
    /// <summary>
    /// 缓存清理
    /// </summary>
    CacheCleanup,

    /// <summary>
    /// 媒体清理
    /// </summary>
    MediaCleanup,

    /// <summary>
    /// 标签向量同步
    /// </summary>
    TagVectorSync,

    /// <summary>
    /// 媒体向量同步
    /// </summary>
    MediaVectorSync,

    /// <summary>
    /// 待入库识别结果清理
    /// </summary>
    PendingIdentificationCleanup,

    // 其他任务
    /// <summary>
    /// 自定义任务
    /// </summary>
    Custom
}
