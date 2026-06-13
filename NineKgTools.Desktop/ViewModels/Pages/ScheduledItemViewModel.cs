using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Desktop.Services;
using NineKgTools.Utils;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// "定时" Tab 行 VM——展示 ScheduledTaskConfig + Hangfire 算出的下次/上次执行时间。
/// 支持手动触发（"立即执行"），由 BackgroundTasksViewModel.TriggerScheduledCommand 驱动，
/// IsTriggering 控制按钮 loading 态。启用切换 / cron 编辑留后续（需写回 yaml + cron 校验）。
/// </summary>
public partial class ScheduledItemViewModel : ObservableObject
{
    /// <param name="nextRunUtc">Hangfire 调度器算出的下次执行时间（UTC）；未注册 / 已禁用为 null</param>
    /// <param name="lastRunUtc">Hangfire 记录的上次执行时间（UTC）；从未执行为 null</param>
    public ScheduledItemViewModel(ScheduledTaskConfig config, DateTime? nextRunUtc, DateTime? lastRunUtc)
    {
        Name = config.Name;
        Type = config.Type;
        Description = string.IsNullOrEmpty(config.Description) ? "（无描述）" : config.Description;
        CronExpression = config.CronExpression;
        Enabled = config.Enabled;
        EnabledText = config.Enabled ? "已启用" : "已禁用";
        TimeoutText = config.TimeoutOverride.HasValue
            ? $"{config.TimeoutOverride.Value} 分钟"
            : "默认";

        StatusBrush = ResourceLookup.Brush(config.Enabled
            ? "SystemFillColorSuccessBrush"
            : "TextFillColorTertiaryBrush");

        NextRunText = BuildNextRunText(config.Enabled, nextRunUtc, config.CronExpression);
        LastRunText = lastRunUtc.HasValue
            ? lastRunUtc.Value.ToLocalTime().ToString("MM-dd HH:mm")
            : "从未执行";
    }

    public string Name { get; }
    public string Type { get; }
    public string Description { get; }
    public string CronExpression { get; }
    public bool Enabled { get; }
    public string EnabledText { get; }
    public string TimeoutText { get; }
    public IBrush? StatusBrush { get; }

    /// <summary>下次执行时间提示（含相对时间），已禁用 = "已禁用"</summary>
    public string NextRunText { get; }

    /// <summary>上次执行时间，从未执行 = "从未执行"</summary>
    public string LastRunText { get; }

    /// <summary>仅启用的任务可手动触发（与 Web 端一致）</summary>
    public bool CanTrigger => Enabled;

    /// <summary>手动触发执行中——按钮置灰 + 文案切 "执行中…"</summary>
    [ObservableProperty]
    private bool _isTriggering;

    /// <summary>
    /// 拼接下次执行提示：禁用 → "已禁用"；有 Hangfire 调度时间 → "MM-dd HH:mm · 约 N 后"；
    /// 否则回退到 cron 的人类可读描述。
    /// </summary>
    private static string BuildNextRunText(bool enabled, DateTime? nextRunUtc, string? cron)
    {
        if (!enabled) return "已禁用";
        if (nextRunUtc.HasValue)
        {
            var local = nextRunUtc.Value.ToLocalTime();
            var rel = HumanizeRelative(nextRunUtc.Value - DateTime.UtcNow);
            return rel is null ? local.ToString("MM-dd HH:mm") : $"{local:MM-dd HH:mm} · {rel}";
        }
        return CronValidator.GetDescription(cron);
    }

    /// <summary>把"距现在多久"humanize 成"约 X 后"；已过期 / 太近返回 null（只显示绝对时间）</summary>
    private static string? HumanizeRelative(TimeSpan delta)
    {
        if (delta <= TimeSpan.Zero) return null;
        if (delta.TotalMinutes < 1) return "即将";
        if (delta.TotalMinutes < 60) return $"约 {(int)delta.TotalMinutes} 分钟后";
        if (delta.TotalHours < 24) return $"约 {(int)delta.TotalHours} 小时后";
        return $"约 {(int)delta.TotalDays} 天后";
    }
}
