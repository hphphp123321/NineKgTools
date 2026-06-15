using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class MediaFilterDialog : UserControl
{
    public MediaFilterDialog() => InitializeComponent();

    /// <summary>
    /// 筛选结果。所有字段都可空（null = "不限"）。
    ///
    /// HasAnyFilter 为 false 时调用方可视为"用户没设任何过滤"——保留默认查询条件。
    /// </summary>
    public sealed record Result(
        bool OnlyTopCategory,
        TopCategory SelectedTopCategoryFilter,
        IReadOnlyList<Category> SelectedCategories,
        string? SelectedTagName,
        string? SelectedFavoriteName,
        float? MinRating,
        string DateFilterType,
        DateTime? StartDate,
        DateTime? EndDate)
    {
        public bool HasCategoryFilter => OnlyTopCategory
            ? SelectedTopCategoryFilter != TopCategory.Unknown
            : SelectedCategories.Count > 0;

        public bool HasAnyFilter =>
            HasCategoryFilter
            || !string.IsNullOrEmpty(SelectedTagName)
            || !string.IsNullOrEmpty(SelectedFavoriteName)
            || MinRating.HasValue
            || StartDate.HasValue
            || EndDate.HasValue;
    }

    /// <summary>
    /// 弹出多维筛选对话框。返回 null = 取消；返回 Result 即用户应用了筛选（可能完全空 = 等价于"重置"）。
    /// </summary>
    public static async Task<Result?> ShowAsync(
        TopCategory currentTopCategory,
        IEnumerable<Tag> allTags,
        IEnumerable<Favorite> allFavorites,
        bool initialOnlyTop = false,
        TopCategory initialSelectedTopFilter = TopCategory.Unknown,
        IReadOnlyList<Category>? initialSelectedCategories = null,
        string? initialSelectedTagName = null,
        string? initialSelectedFavoriteName = null,
        float? initialMinRating = null,
        string initialDateFilterType = "ReleaseDate",
        DateTime? initialStartDate = null,
        DateTime? initialEndDate = null)
    {
        var ctx = new MediaFilterDialogContext(currentTopCategory, allTags, allFavorites);
        ctx.Initialize(
            initialOnlyTop,
            initialSelectedTopFilter,
            initialSelectedCategories ?? Array.Empty<Category>(),
            initialSelectedTagName,
            initialSelectedFavoriteName,
            initialMinRating,
            initialDateFilterType,
            initialStartDate,
            initialEndDate);

        var view = new MediaFilterDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = "应用筛选",
            SecondaryButtonText = "重置",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
        };

        // 重置：阻止关闭，只清空字段
        dialog.SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            ctx.Reset();
        };

        var dlgResult = await dialog.ShowAsync();
        if (dlgResult != FAContentDialogResult.Primary) return null;

        return new Result(
            ctx.OnlyTopCategory,
            ctx.OnlyTopCategory
                ? (ctx.SelectedTopCategory?.Value ?? TopCategory.Unknown)
                : TopCategory.Unknown,
            ctx.OnlyTopCategory ? Array.Empty<Category>() : ctx.CollectSelectedCategories(),
            string.IsNullOrEmpty(ctx.SelectedTagName) ? null : ctx.SelectedTagName,
            string.IsNullOrEmpty(ctx.SelectedFavoriteName) ? null : ctx.SelectedFavoriteName,
            ctx.SelectedRating?.Value,
            ctx.SelectedDateFilterType?.Value ?? "ReleaseDate",
            ctx.StartDate?.DateTime,
            ctx.EndDate?.DateTime);
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
                    Text = "🔍",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "筛选媒体",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
