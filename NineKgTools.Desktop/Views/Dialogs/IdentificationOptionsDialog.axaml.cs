using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class IdentificationOptionsDialog : UserControl
{
    public IdentificationOptionsDialog() => InitializeComponent();

    /// <summary>
    /// 弹出"识别选项"对话框。返回非 null = 用户点了"开始识别"且通过验证；
    /// null = 用户取消 / 关闭。重置按钮通过 SecondaryButtonClick 截获，不会关闭对话框。
    /// </summary>
    /// <param name="sourcePath">要识别的路径（仅展示，最终也会写入返回的 Options.SourcePath）</param>
    /// <param name="initialOptions">默认值（通常来自 <c>Config.Identification.ToIdentificationOptions()</c>，
    /// 调用方按需调整 SkipCache / AutoAddToDatabase）</param>
    /// <param name="availableWebsites">下拉框里展示的站点名（来自 <c>WebsiteService.WebsiteNameMap.Keys</c>）</param>
    /// <param name="isReidentify">true = 重新识别（标题"重新识别选项"）；false = 首次手动识别（标题"手动识别选项"）</param>
    public static async Task<IdentificationOptions?> ShowAsync(
        string sourcePath,
        IdentificationOptions initialOptions,
        IReadOnlyList<string> availableWebsites,
        bool isReidentify)
    {
        var ctx = new IdentificationOptionsDialogContext(sourcePath, initialOptions, availableWebsites, isReidentify);
        var view = new IdentificationOptionsDialog { DataContext = ctx };

        IdentificationOptions? confirmed = null;

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(ctx),
            Content = view,
            PrimaryButtonText = "开始识别",
            SecondaryButtonText = "重置为默认",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };

        // 主按钮：验证 + 落 options；验证失败用 Cancel=true 留住 dialog 让用户改
        dialog.PrimaryButtonClick += (s, args) =>
        {
            var built = ctx.BuildOptions();
            if (built == null)
            {
                args.Cancel = true;
                return;
            }
            confirmed = built;
        };

        // 次按钮：重置，不允许关闭
        dialog.SecondaryButtonClick += (s, args) =>
        {
            args.Cancel = true;
            ctx.ResetCommand.Execute(null);
        };

        await dialog.ShowAsync();
        return confirmed;
    }

    private static Control BuildTitleVisual(IdentificationOptionsDialogContext ctx)
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
                    Text = "🔍",
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
