using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class TagEditorDialog : UserControl
{
    public static readonly StyledProperty<string> NameValueProperty =
        AvaloniaProperty.Register<TagEditorDialog, string>(nameof(NameValue), "");

    public static readonly StyledProperty<string> DescriptionValueProperty =
        AvaloniaProperty.Register<TagEditorDialog, string>(nameof(DescriptionValue), "");

    public static readonly StyledProperty<TopTag?> SelectedTopTagProperty =
        AvaloniaProperty.Register<TagEditorDialog, TopTag?>(nameof(SelectedTopTag));

    public static readonly StyledProperty<ObservableCollection<TopTag>> AvailableTopTagsProperty =
        AvaloniaProperty.Register<TagEditorDialog, ObservableCollection<TopTag>>(
            nameof(AvailableTopTags), new ObservableCollection<TopTag>());

    public static readonly StyledProperty<bool> ShowEditHintProperty =
        AvaloniaProperty.Register<TagEditorDialog, bool>(nameof(ShowEditHint));

    public static readonly StyledProperty<string> EditHintTextProperty =
        AvaloniaProperty.Register<TagEditorDialog, string>(nameof(EditHintText), "");

    public string NameValue
    {
        get => GetValue(NameValueProperty);
        set => SetValue(NameValueProperty, value);
    }

    public string DescriptionValue
    {
        get => GetValue(DescriptionValueProperty);
        set => SetValue(DescriptionValueProperty, value);
    }

    public TopTag? SelectedTopTag
    {
        get => GetValue(SelectedTopTagProperty);
        set => SetValue(SelectedTopTagProperty, value);
    }

    public ObservableCollection<TopTag> AvailableTopTags
    {
        get => GetValue(AvailableTopTagsProperty);
        set => SetValue(AvailableTopTagsProperty, value);
    }

    public bool ShowEditHint
    {
        get => GetValue(ShowEditHintProperty);
        set => SetValue(ShowEditHintProperty, value);
    }

    public string EditHintText
    {
        get => GetValue(EditHintTextProperty);
        set => SetValue(EditHintTextProperty, value);
    }

    public TagEditorDialog() => InitializeComponent();

    public record Result(string Name, string? Description, TopTag TopTag);

    /// <summary>
    /// 弹出标签编辑器；返回非 null 表示用户确认，null = 取消或表单无效。
    /// </summary>
    public static async Task<Result?> ShowAsync(
        string title,
        IReadOnlyList<TopTag> availableTopTags,
        TopTag? initialTopTag = null,
        string? initialName = null,
        string? initialDescription = null,
        int? mediaCount = null)
    {
        var topTagsCollection = new ObservableCollection<TopTag>(availableTopTags);

        // 解析当前选中：优先按 Id 在新 collection 里找，避免引用不一致
        TopTag? preselected = null;
        if (initialTopTag is not null)
        {
            preselected = topTagsCollection.FirstOrDefault(t => t.Id == initialTopTag.Id)
                          ?? topTagsCollection.FirstOrDefault();
        }
        else
        {
            preselected = topTagsCollection.FirstOrDefault();
        }

        var view = new TagEditorDialog
        {
            NameValue = initialName ?? "",
            DescriptionValue = initialDescription ?? "",
            AvailableTopTags = topTagsCollection,
            SelectedTopTag = preselected,
            ShowEditHint = mediaCount is > 0,
            EditHintText = mediaCount is > 0
                ? $"此标签关联了 {mediaCount} 条媒体。修改名称会同步影响这些媒体的标签展示。"
                : ""
        };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(title, isEdit: initialName is not null),
            Content = view,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        // 表单有效条件：名字非空 + 选了顶级
        void RecomputeValidity()
        {
            dialog.IsPrimaryButtonEnabled =
                !string.IsNullOrWhiteSpace(view.NameValue) && view.SelectedTopTag is not null;
        }

        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == NameValueProperty || e.Property == SelectedTopTagProperty)
                RecomputeValidity();
        };
        RecomputeValidity();

        var dlgResult = await dialog.ShowAsync();
        if (dlgResult != FAContentDialogResult.Primary) return null;

        var name = view.NameValue?.Trim();
        var top = view.SelectedTopTag;
        if (string.IsNullOrEmpty(name) || top is null) return null;

        var desc = string.IsNullOrWhiteSpace(view.DescriptionValue) ? null : view.DescriptionValue.Trim();
        return new Result(name, desc, top);
    }

    private static Control BuildTitleVisual(string title, bool isEdit)
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
                    Text = isEdit ? "✏️" : "✚",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = title,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
