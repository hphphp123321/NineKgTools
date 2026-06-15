using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class FavoriteSelectorDialog : UserControl
{
    public FavoriteSelectorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出收藏夹选择器。多选 / 单选共用——多选模式下可提交空列表（等价"清空收藏关联"）；
    /// 单选必须选 1 项。
    /// </summary>
    public static async Task<List<Favorite>?> ShowAsync(
        IReadOnlyList<Favorite> initialSelected,
        bool allowMultiSelect,
        FavoriteService favoriteService)
    {
        ArgumentNullException.ThrowIfNull(favoriteService);

        var ctx = new FavoriteSelectorDialogContext(allowMultiSelect)
        {
            IsLoading = true,
        };
        var view = new FavoriteSelectorDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(allowMultiSelect),
            Content = view,
            PrimaryButtonText = ctx.ConfirmText,
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        ctx.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(ctx.CanSubmit):
                    dialog.IsPrimaryButtonEnabled = ctx.CanSubmit;
                    break;
                case nameof(ctx.ConfirmText):
                    dialog.PrimaryButtonText = ctx.ConfirmText;
                    break;
            }
        };

        // 异步加载全部收藏夹
        _ = Task.Run(async () =>
        {
            try
            {
                var all = await favoriteService.GetAllFavoritesAsync();
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ctx.Initialize(all, initialSelected);
                    ctx.IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FavoriteSelectorDialog 加载收藏夹失败");
                await Dispatcher.UIThread.InvokeAsync(() => ctx.IsLoading = false);
            }
        });

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;
        return ctx.CollectSelected();
    }

    private static Control BuildTitleVisual(bool allowMultiSelect)
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
                    Text = "★",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = allowMultiSelect ? "选择收藏夹（多选）" : "选择收藏夹",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
