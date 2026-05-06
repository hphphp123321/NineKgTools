using Avalonia.Media;
using NineKgTools.Core.Services.Configs;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// "定时" Tab 行 VM——只读展示 ScheduledTaskConfig。
/// 启用切换 / cron 编辑留 P3 后续（需写回 yaml + cron 校验）。
/// </summary>
public sealed class ScheduledItemViewModel
{
    public ScheduledItemViewModel(ScheduledTaskConfig config)
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

        StatusBrush = config.Enabled
            ? AppBrush("SystemFillColorSuccessBrush")
            : AppBrush("TextFillColorTertiaryBrush");
    }

    public string Name { get; }
    public string Type { get; }
    public string Description { get; }
    public string CronExpression { get; }
    public bool Enabled { get; }
    public string EnabledText { get; }
    public string TimeoutText { get; }
    public IBrush? StatusBrush { get; }

    private static IBrush? AppBrush(string key)
    {
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                key, Avalonia.Application.Current.ActualThemeVariant, out var obj) == true
            && obj is IBrush b)
        {
            return b;
        }
        return null;
    }
}
