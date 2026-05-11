using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Tasks.Interfaces;

namespace NineKgTools.Core.Services.Tasks.Progress;

/// <summary>
/// 把 IProgressReporter 的回调统一桥接成一个 <see cref="OnProgress"/> 事件，便于 UI 层
/// （Web 的 IdentificationLoadingDialog / Desktop 的 IdentificationProgressDialog）订阅渲染。
///
/// 与 Hangfire 的 TaskProgressService 互补：那个走 db / 后台任务持久化，这个是
/// "前台同步识别 + 实时进度反馈"的轻量进度桥。
/// </summary>
public class DialogProgressReporter : IProgressReporter
{
    /// <summary>每次进度更新触发；包含完整的 <see cref="TaskLogEntry"/>（含 Message/Progress/Level/CurrentItem）。</summary>
    public event Action<TaskLogEntry>? OnProgress;

    private double _currentProgress;

    public Task ReportAsync(
        string message,
        double? progress = null,
        TaskLogLevel level = TaskLogLevel.Info,
        string? currentItem = null,
        string? phase = null,
        object? extraData = null)
    {
        if (progress.HasValue)
            _currentProgress = progress.Value;

        var entry = new TaskLogEntry
        {
            Message = message,
            Progress = progress ?? _currentProgress,
            Level = level,
            CurrentItem = currentItem,
        };

        OnProgress?.Invoke(entry);
        return Task.CompletedTask;
    }

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

    public Task StartAsync(string message, int? totalItems = null)
    {
        _currentProgress = 0;
        return ReportAsync(message, 0, TaskLogLevel.Info);
    }

    public Task PhaseAsync(string phase, double progress, string? message = null)
        => ReportAsync(message ?? phase, progress, TaskLogLevel.Info, phase: phase);

    public Task CompleteAsync(string message, int? processedItems = null, int? failedItems = null)
        => ReportAsync(message, 100, TaskLogLevel.Success);

    public Task FailAsync(string error, Exception? exception = null)
        => ReportAsync(error, null, TaskLogLevel.Error, extraData: exception?.ToString());
}
