using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tasks.Base;
using Serilog;

namespace NineKgTools.Core.Services.Tasks.ScheduledTasks;

/// <summary>
/// 待入库识别结果清理任务。
/// 定期清理 PendingIdentification 表中超过保留期限（IdentificationConfig.PendingRetentionDays）的记录，
/// 并把对应 MediaSource 的 Identified 标记置回 false，使其回到"待识别"状态。
/// 用户可以在 Settings 里修改 PendingRetentionDays；0 表示永不清理，此时任务执行即视为空操作。
/// </summary>
[ScheduledTask("PendingIdentificationCleanup", "待入库记录清理", TaskType.PendingIdentificationCleanup)]
public class PendingIdentificationCleanupTask : ScheduledTaskBase
{
    private readonly PendingIdentificationService _pendingIdentificationService;
    private readonly Config _config;

    public override TaskType TaskType => TaskType.PendingIdentificationCleanup;
    public override string TaskName => "待入库记录清理任务";
    public override string? TaskDescription => "清理超过保留期限的待入库识别结果，把对应媒体源回到待识别状态";

    public PendingIdentificationCleanupTask(
        PendingIdentificationService pendingIdentificationService,
        Config config)
    {
        _pendingIdentificationService = pendingIdentificationService;
        _config = config;
    }

    public override async Task<TaskResult> ExecuteAsync(
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 优先使用 parameters 覆盖值（便于 UI 手动触发时临时指定），否则走 Config 默认
            var retentionDays = GetRetentionDays(parameters);

            if (retentionDays <= 0)
            {
                Log.Information("PendingIdentificationCleanup 跳过：PendingRetentionDays <= 0（永不清理）");
                return TaskResult.CreateSuccess(
                    TaskId, TaskName, "已跳过（保留天数配置为 0）", 0, TaskType);
            }

            Log.Information("开始清理待入库识别结果，保留天数={Days}", retentionDays);

            var removed = await _pendingIdentificationService.CleanupExpiredAsync(retentionDays, cancellationToken);

            var message = removed > 0
                ? $"已清理 {removed} 条超期待入库记录（保留天数={retentionDays}）"
                : $"没有需要清理的待入库记录（保留天数={retentionDays}）";

            Log.Information(message);
            return TaskResult.CreateSuccess(TaskId, TaskName, message, removed, TaskType);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "待入库记录清理任务执行失败");
            return TaskResult.CreateFailure(TaskId, TaskName, $"任务执行失败: {ex.Message}", ex);
        }
    }

    public override bool ValidateParameters(Dictionary<string, object>? parameters)
    {
        if (parameters == null || parameters.Count == 0) return true;

        if (parameters.TryGetValue("retention_days", out var value))
            return value is int or long or double;

        return true;
    }

    /// <summary>
    /// 读取保留天数：parameters.retention_days 优先，否则回退到 Config。
    /// </summary>
    private int GetRetentionDays(Dictionary<string, object>? parameters)
    {
        if (parameters?.TryGetValue("retention_days", out var value) == true)
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                _ => _config.Identification?.PendingRetentionDays ?? 0
            };
        }

        return _config.Identification?.PendingRetentionDays ?? 0;
    }
}
