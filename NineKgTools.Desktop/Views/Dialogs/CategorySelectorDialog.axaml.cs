using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class CategorySelectorDialog : UserControl
{
    public CategorySelectorDialog() => InitializeComponent();

    /// <summary>
    /// 分类选择结果。<see cref="OnlyTopCategory"/>=true 时只看 <see cref="SelectedTopCategory"/>；
    /// 否则看 <see cref="SelectedCategories"/>。HasFilter / GetDisplayText 给调用方做摘要。
    /// </summary>
    public sealed record Result(
        bool OnlyTopCategory,
        TopCategory SelectedTopCategory,
        IReadOnlyList<Category> SelectedCategories)
    {
        public bool HasFilter => OnlyTopCategory
            ? SelectedTopCategory != TopCategory.Unknown
            : SelectedCategories.Count > 0;

        public string DisplayText
        {
            get
            {
                if (OnlyTopCategory && SelectedTopCategory != TopCategory.Unknown)
                {
                    return SelectedTopCategory switch
                    {
                        TopCategory.Video => "全部视频",
                        TopCategory.Audio => "全部音频",
                        TopCategory.Picture => "全部图片",
                        TopCategory.Text => "全部文本",
                        TopCategory.Game => "全部游戏",
                        _ => "",
                    };
                }
                if (SelectedCategories.Count == 0) return "";
                if (SelectedCategories.Count == 1) return SelectedCategories[0].Name;
                return $"{SelectedCategories[0].Name} 等 {SelectedCategories.Count} 个分类";
            }
        }
    }

    /// <summary>
    /// 弹出分类选择器。null = 取消；用户点"清除选择"返回空 Result（HasFilter=false）。
    /// </summary>
    public static async Task<Result?> ShowAsync(
        TopCategory filterTopCategory = TopCategory.Unknown,
        IReadOnlyList<Category>? initialSelected = null,
        bool initialOnlyTop = false,
        TopCategory? initialSelectedTop = null)
    {
        var ctx = new CategorySelectorDialogContext(filterTopCategory);
        ctx.Initialize(initialSelectedTop, initialSelected ?? Array.Empty<Category>(), initialOnlyTop);

        var view = new CategorySelectorDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = "确定",
            SecondaryButtonText = "清除选择",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = ctx.CanSubmit,
        };

        ctx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ctx.CanSubmit))
                dialog.IsPrimaryButtonEnabled = ctx.CanSubmit;
        };

        var dlgResult = await dialog.ShowAsync();

        if (dlgResult == FAContentDialogResult.Primary)
        {
            return new Result(
                ctx.OnlyTopCategory,
                ctx.SelectedTopCategory?.Value ?? TopCategory.Unknown,
                ctx.OnlyTopCategory ? Array.Empty<Category>() : ctx.CollectSelectedCategories());
        }

        if (dlgResult == FAContentDialogResult.Secondary)
        {
            // 清除选择 = 返回空过滤
            return new Result(false, TopCategory.Unknown, Array.Empty<Category>());
        }

        return null; // Cancel
    }

    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CategorySelectorDialogContext ctx) ctx.SelectAll();
    }

    private void OnDeselectAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CategorySelectorDialogContext ctx) ctx.DeselectAll();
    }

    private static Control BuildTitleVisual()
    {
        IBrush iconBrush = Brushes.Gray;
        if (Application.Current?.Resources.TryGetResource(
                "SystemFillColorAttentionBrush",
                Application.Current.ActualThemeVariant,
                out var brushObj) == true
            && brushObj is IBrush b)
        {
            iconBrush = b;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "📂",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "选择分类",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
