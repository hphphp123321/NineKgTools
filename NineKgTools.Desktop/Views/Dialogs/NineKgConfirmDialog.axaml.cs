using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class NineKgConfirmDialog : UserControl
{
    public NineKgConfirmDialog() => InitializeComponent();

    /// <summary>
    /// 显示一个共享的确认对话框，返回 true 表示用户点了主操作按钮，false 表示取消。
    ///
    /// 用法：
    /// <code>
    ///   var ok = await NineKgConfirmDialog.ShowAsync(this, "确认删除", "你将永久删除...",
    ///                intent: DialogIntent.Destructive, targetName: "视频名");
    ///   if (ok) await DeleteAsync();
    /// </code>
    /// </summary>
    public static async Task<bool> ShowAsync(
        Visual? ownerVisual,
        string title,
        string message,
        DialogIntent intent = DialogIntent.Info,
        string? confirmText = null,
        string? cancelText = null,
        string? targetName = null,
        int? affectedCount = null,
        IReadOnlyList<string>? targetItems = null)
    {
        var context = new NineKgConfirmDialogContext
        {
            Intent = intent,
            Title = title,
            Message = message,
            TargetName = targetName,
            AffectedCount = affectedCount,
            TargetItems = targetItems,
        };

        var view = new NineKgConfirmDialog
        {
            DataContext = context,
        };

        var dialog = new FAContentDialog
        {
            // Title slot 用自渲染的"图标 + 标题"——避免 Title=null 时的空白占位
            Title = BuildTitleVisual(context),
            Content = view,
            PrimaryButtonText = confirmText ?? context.DefaultConfirmText,
            CloseButtonText = cancelText ?? "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result == FAContentDialogResult.Primary;
    }

    /// <summary>
    /// 构造 ContentDialog.Title slot 的内容：左侧图标（带 Intent 语义色）+ 右侧标题文字。
    /// </summary>
    private static Control BuildTitleVisual(NineKgConfirmDialogContext ctx)
    {
        // 取系统语义 brush 给图标着色（Info=蓝/Affirmative=绿/Destructive=红）
        IBrush iconBrush = ResourceLookup.Brush(ctx.AccentBrushKey) ?? Brushes.Gray;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = ctx.IconGlyph,
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = ctx.Title,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
