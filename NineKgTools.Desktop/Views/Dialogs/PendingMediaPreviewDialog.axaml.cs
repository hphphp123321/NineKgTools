using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class PendingMediaPreviewDialog : UserControl
{
    public PendingMediaPreviewDialog() => InitializeComponent();

    /// <summary>
    /// 弹出"待入库识别结果"预览对话框。返回 true = 用户在预览界面点了"确认入库"；
    /// 否则（关闭 / 取消）返回 false——调用方仅做关闭，无需 RefreshAsync。
    /// </summary>
    public static async Task<bool> ShowAsync(MediaBase media, MediaSource? source)
    {
        ArgumentNullException.ThrowIfNull(media);

        var ctx = new PendingMediaPreviewDialogContext(media, source);
        var view = new PendingMediaPreviewDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = "确认入库",
            CloseButtonText = "关闭",
            DefaultButton = FAContentDialogButton.Primary,
        };

        var result = await dialog.ShowAsync();
        return result == FAContentDialogResult.Primary;
    }

    private static Control BuildTitleVisual()
    {
        IBrush iconBrush = ResourceLookup.Brush("SystemFillColorAttentionBrush") ?? Brushes.Gray;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "👁",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "预览待入库识别结果",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
