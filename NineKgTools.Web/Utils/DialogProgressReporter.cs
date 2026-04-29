using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;

namespace NineKgTools.Utils;

/// <summary>
/// 用于对话框的进度报告器，通过事件将进度更新传递给UI组件
/// </summary>
public class DialogProgressReporter : IProgressReporter
{
    #region 统一事件

    /// <summary>
    /// 统一进度事件 - 每次进度更新都会触发，包含完整的进度信息
    /// </summary>
    public event Action<TaskLogEntry>? OnProgress;

    #endregion

    #region 内部状态

    private double _currentProgress;
    private string? _currentPhase;
    private int _totalItems;
    private int _processedItems;

    #endregion

    #region 核心统一方法实现

    public Task ReportAsync(
        string message,
        double? progress = null,
        TaskLogLevel level = TaskLogLevel.Info,
        string? currentItem = null,
        string? phase = null,
        object? extraData = null)
    {
        // 更新内部状态
        if (progress.HasValue)
            _currentProgress = progress.Value;
        if (phase != null)
            _currentPhase = phase;

        var entry = new TaskLogEntry
        {
            Message = message,
            Progress = progress ?? _currentProgress,
            Level = level,
            CurrentItem = currentItem
        };

        // 触发统一事件
        OnProgress?.Invoke(entry);

        return Task.CompletedTask;
    }

    #endregion

    #region 便捷方法实现

    public Task InfoAsync(string message, double progress, string? currentItem = null)
        => ReportAsync(message, progress, TaskLogLevel.Info, currentItem);

    public Task WarningAsync(string message, double? progress = null, string? currentItem = null)
        => ReportAsync(message, progress, TaskLogLevel.Warning, currentItem);

    public Task ErrorAsync(string message, double? progress = null, string? currentItem = null)
        => ReportAsync(message, progress, TaskLogLevel.Error, currentItem);

    public Task SuccessAsync(string message, double? progress = null, string? currentItem = null)
        => ReportAsync(message, progress, TaskLogLevel.Success, currentItem);

    public Task DebugAsync(string message, string? currentItem = null)
        => ReportAsync(message, null, TaskLogLevel.Debug, currentItem);

    #endregion

    #region 生命周期方法实现

    public Task StartAsync(string message, int? totalItems = null)
    {
        _currentProgress = 0;
        _totalItems = totalItems ?? 0;
        _processedItems = 0;
        _currentPhase = null;

        return ReportAsync(message, 0, TaskLogLevel.Info);
    }

    public Task PhaseAsync(string phase, double progress, string? message = null)
    {
        _currentPhase = phase;
        return ReportAsync(message ?? phase, progress, TaskLogLevel.Info, phase: phase);
    }

    public Task CompleteAsync(string message, int? processedItems = null, int? failedItems = null)
    {
        _processedItems = processedItems ?? _processedItems;
        return ReportAsync(message, 100, TaskLogLevel.Success);
    }

    public Task FailAsync(string error, Exception? exception = null)
    {
        return ReportAsync(error, null, TaskLogLevel.Error, extraData: exception?.ToString());
    }

    #endregion
}
