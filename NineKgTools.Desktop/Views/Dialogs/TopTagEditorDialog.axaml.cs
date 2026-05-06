using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class TopTagEditorDialog : UserControl
{
    public static readonly StyledProperty<string> NameValueProperty =
        AvaloniaProperty.Register<TopTagEditorDialog, string>(nameof(NameValue), "");

    public static readonly StyledProperty<bool> ShowEditHintProperty =
        AvaloniaProperty.Register<TopTagEditorDialog, bool>(nameof(ShowEditHint));

    public static readonly StyledProperty<string> EditHintTextProperty =
        AvaloniaProperty.Register<TopTagEditorDialog, string>(nameof(EditHintText), "");

    public string NameValue
    {
        get => GetValue(NameValueProperty);
        set => SetValue(NameValueProperty, value);
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

    public TopTagEditorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出顶级标签编辑器；返回非 null 表示用户确认（含填好的名字），null = 取消。
    /// </summary>
    public static async Task<string?> ShowAsync(
        string title,
        string? initialName = null,
        int? childCount = null)
    {
        var view = new TopTagEditorDialog
        {
            NameValue = initialName ?? "",
            ShowEditHint = childCount is > 0,
            EditHintText = childCount is > 0
                ? $"此分组下有 {childCount} 个子标签。重命名不影响关联关系。"
                : ""
        };

        var dialog = new ContentDialog
        {
            Title = BuildTitleVisual(title, isEdit: initialName is not null),
            Content = view,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(view.NameValue),
        };

        // 名字为空时主按钮禁用
        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == NameValueProperty)
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(view.NameValue);
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        var trimmed = view.NameValue?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static Control BuildTitleVisual(string title, bool isEdit)
    {
        IBrush iconBrush = Brushes.Gray;
        if (Application.Current?.Resources.TryGetResource(
                "SystemFillColorAttentionBrush", Application.Current.ActualThemeVariant, out var b) == true
            && b is IBrush br)
        {
            iconBrush = br;
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
