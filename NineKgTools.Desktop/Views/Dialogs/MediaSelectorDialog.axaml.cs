using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class MediaSelectorDialog : UserControl
{
    public MediaSelectorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出媒体多选对话框。返回选中的 Id 列表（空列表 = 用户清空所有；null = 用户取消）。
    ///
    /// 调用方典型用法（与 Web ProcessRelatedMediasSelection 同款 diff 流程）：
    /// <code>
    ///   var ids = await MediaSelectorDialog.ShowAsync(mediaService, imageCache,
    ///       excludeMediaId: currentMedia.Id, initialSelected: currentMedia.RelatedMedias);
    ///   if (ids is null) return; // 用户取消
    ///   var currentIds = currentMedia.RelatedMedias.Select(m => m.Id).ToHashSet();
    ///   var selectedSet = ids.ToHashSet();
    ///   foreach (var id in selectedSet.Except(currentIds)) await mediaService.AddRelatedMediaAsync(currentMedia.Id, id);
    ///   foreach (var id in currentIds.Except(selectedSet)) await mediaService.RemoveRelatedMediaAsync(currentMedia.Id, id);
    /// </code>
    /// </summary>
    public static async Task<System.Collections.Generic.List<int>?> ShowAsync(
        MediaService mediaService,
        ImageCacheService imageCache,
        int? excludeMediaId,
        System.Collections.Generic.IReadOnlyList<MediaBase> initialSelected)
    {
        ArgumentNullException.ThrowIfNull(mediaService);
        ArgumentNullException.ThrowIfNull(imageCache);
        initialSelected ??= System.Array.Empty<MediaBase>();

        var ctx = new MediaSelectorDialogContext(mediaService, imageCache, excludeMediaId, initialSelected);
        var view = new MediaSelectorDialog { DataContext = ctx };

        System.Collections.Generic.List<int>? confirmed = null;

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = ctx.ConfirmText,
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };
        // FluentAvalonia 默认 ContentDialogMaxWidth≈548，比卡片网格内容（MinWidth 700）窄，
        // 外框卡死导致内容左右溢出裁切（搜索框 placeholder 被切左半）。撑大外框消除裁切。
        dialog.Resources["ContentDialogMaxWidth"] = 900d;

        // 同步 ctx → dialog 主按钮文字（数量变化时刷新）
        ctx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ctx.ConfirmText))
                dialog.PrimaryButtonText = ctx.ConfirmText;
        };

        dialog.PrimaryButtonClick += (_, _) =>
        {
            confirmed = ctx.GetSelectedIds();
        };

        await dialog.ShowAsync();
        return confirmed;
    }

    private static Control BuildTitleVisual()
    {
        IBrush iconBrush = ResourceLookup.Brush("AccentFillColorDefaultBrush") ?? Brushes.SteelBlue;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "🔗", FontSize = 22, Foreground = iconBrush, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "选择相关媒体", VerticalAlignment = VerticalAlignment.Center },
            }
        };
    }
}
