using System;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Core.Services.Tasks.Interfaces;

/// <summary>
/// 任务进度报告接口 - 统一进度和日志报告
/// </summary>
public interface IProgressReporter
{
    #region 核心统一方法

    /// <summary>
    /// 报告进度（统一入口）- 同时更新进度和记录日志
    /// </summary>
    /// <param name="message">进度消息</param>
    /// <param name="progress">进度百分比 (0-100)，null 表示不更新进度</param>
    /// <param name="level">日志级别</param>
    /// <param name="currentItem">当前处理项</param>
    /// <param name="phase">当前阶段</param>
    /// <param name="extraData">额外数据</param>
    Task ReportAsync(
        string message,
        double? progress = null,
        TaskLogLevel level = TaskLogLevel.Info,
        string? currentItem = null,
        string? phase = null,
        object? extraData = null);

    #endregion

    #region 便捷方法

    /// <summary>
    /// 信息级别，带进度更新
    /// </summary>
    Task InfoAsync(string message, double progress, string? currentItem = null);

    /// <summary>
    /// 警告级别
    /// </summary>
    Task WarningAsync(string message, double? progress = null, string? currentItem = null);

    /// <summary>
    /// 错误级别
    /// </summary>
    Task ErrorAsync(string message, double? progress = null, string? currentItem = null);

    /// <summary>
    /// 成功级别
    /// </summary>
    Task SuccessAsync(string message, double? progress = null, string? currentItem = null);

    /// <summary>
    /// 调试级别（不更新进度）
    /// </summary>
    Task DebugAsync(string message, string? currentItem = null);

    #endregion

    #region 生命周期方法

    /// <summary>
    /// 报告任务开始
    /// </summary>
    /// <param name="message">开始消息</param>
    /// <param name="totalItems">总项数（可选）</param>
    Task StartAsync(string message, int? totalItems = null);

    /// <summary>
    /// 报告进入新阶段
    /// </summary>
    /// <param name="phase">阶段名称</param>
    /// <param name="progress">阶段进度</param>
    /// <param name="message">阶段消息（可选，默认使用阶段名称）</param>
    Task PhaseAsync(string phase, double progress, string? message = null);

    /// <summary>
    /// 报告任务完成
    /// </summary>
    /// <param name="message">完成消息</param>
    /// <param name="processedItems">已处理项数（可选）</param>
    /// <param name="failedItems">失败项数（可选）</param>
    Task CompleteAsync(string message, int? processedItems = null, int? failedItems = null);

    /// <summary>
    /// 报告任务失败
    /// </summary>
    /// <param name="error">错误信息</param>
    /// <param name="exception">异常（可选）</param>
    Task FailAsync(string error, Exception? exception = null);

    #endregion
}
