using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Desktop.Views.Dialogs;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// NineKgConfirmDialog 的视图上下文。所有计算属性都从 Intent / 数据字段派生，
/// AXAML 直接绑定避免在 code-behind 写大量 if-else。
/// </summary>
public partial class NineKgConfirmDialogContext : ObservableObject
{
    public DialogIntent Intent { get; init; } = DialogIntent.Info;
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public string? TargetName { get; init; }
    public int? AffectedCount { get; init; }
    public IReadOnlyList<string>? TargetItems { get; init; }

    /// <summary>顶部 4px accent 色条的颜色 brush，绑定 SystemFillColor* 系统语义色</summary>
    public string AccentBrushKey => Intent switch
    {
        DialogIntent.Info => "SystemFillColorAttentionBrush",
        DialogIntent.Affirmative => "SystemFillColorSuccessBrush",
        DialogIntent.Destructive => "SystemFillColorCriticalBrush",
        DialogIntent.DestructiveBatch => "SystemFillColorCriticalBrush",
        _ => "SystemFillColorAttentionBrush"
    };

    public string IconGlyph => Intent switch
    {
        DialogIntent.Info => "ⓘ",
        DialogIntent.Affirmative => "✓",
        DialogIntent.Destructive => "⚠",
        DialogIntent.DestructiveBatch => "⚠",
        _ => ""
    };

    public bool ShowWarningBar => Intent is DialogIntent.Destructive or DialogIntent.DestructiveBatch;

    public bool ShowTargetName =>
        Intent == DialogIntent.Destructive && !string.IsNullOrEmpty(TargetName);

    public bool ShowHeroCount =>
        Intent == DialogIntent.DestructiveBatch && AffectedCount.HasValue;

    /// <summary>主操作按钮默认文案（调用方可显式 override）</summary>
    public string DefaultConfirmText => Intent switch
    {
        DialogIntent.Info => "确认",
        DialogIntent.Affirmative => "执行",
        DialogIntent.Destructive => "确认删除",
        DialogIntent.DestructiveBatch => "全部删除",
        _ => "确认"
    };
}
