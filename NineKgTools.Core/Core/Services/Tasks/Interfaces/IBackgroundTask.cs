using System;
using System.Threading;
using System.Threading.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 后台持续运行任务接口
/// 用于标识真正在后台持续运行的任务（如文件夹监控）
/// 与普通识别任务的区别：ExecuteAsync立即返回，任务在后台持续运行
/// </summary>
public interface IBackgroundTask : ITask
{
    /// <summary>
    /// 任务是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 任务启动时间
    /// </summary>
    DateTime? StartedAt { get; }

    /// <summary>
    /// 获取后台任务的统计信息
    /// </summary>
    /// <returns>后台任务统计信息</returns>
    BackgroundTaskStats GetStatistics();

    /// <summary>
    /// 停止后台任务
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>停止操作的结果</returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 后台任务统计信息
/// </summary>
public class BackgroundTaskStats
{
    /// <summary>
    /// 总处理数量
    /// </summary>
    public int TotalProcessed { get; set; }

    /// <summary>
    /// 总失败数量
    /// </summary>
    public int TotalFailed { get; set; }

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime? LastActivityTime { get; set; }

    /// <summary>
    /// 状态消息
    /// </summary>
    public string? StatusMessage { get; set; }
}
