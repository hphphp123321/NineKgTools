using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class TagSelectorDialog : UserControl
{
    public TagSelectorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出标签选择器。多选 / 单选共用同一对话框，由 allowMultiSelect 控制行为。
    /// 用户取消 → null；多选模式确认 → 当前选中列表（可能为空，等价于"清空"）；单选模式确认 → 1 项。
    /// </summary>
    public static async Task<List<Tag>?> ShowAsync(
        IReadOnlyList<Tag> initialSelected,
        bool allowMultiSelect,
        TagService tagService)
    {
        ArgumentNullException.ThrowIfNull(tagService);

        var ctx = new TagSelectorDialogContext(allowMultiSelect)
        {
            IsLoading = true,
        };
        var view = new TagSelectorDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(allowMultiSelect),
            Content = view,
            PrimaryButtonText = ctx.ConfirmText,
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        // 同步 context → dialog 主按钮文字 / 可用态
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

        // 异步加载所有 Tag 并初始化 context
        _ = Task.Run(async () =>
        {
            try
            {
                var allTags = await tagService.GetAllTagsAsync();
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ctx.Initialize(allTags, initialSelected);
                    ctx.IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TagSelectorDialog 加载标签失败");
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ctx.IsLoading = false;
                });
            }
        });

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;

        return ctx.CollectSelected();
    }

    private static Control BuildTitleVisual(bool allowMultiSelect)
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
                    Text = "🏷️",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = allowMultiSelect ? "选择标签（多选）" : "选择标签",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
