using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.Views.Dialogs;

/// <summary>
/// 社团添加 / 重命名对话框。与 Web `CirclesPage` "添加社团" dialog 等价：只一个 Name 字段。
/// 不做重名验证——CreatorService.CreateCircleAsync 同名允许（按 Id 区分）。
/// </summary>
public partial class CircleEditorDialog : UserControl
{
    public static readonly StyledProperty<string> NameValueProperty =
        AvaloniaProperty.Register<CircleEditorDialog, string>(nameof(NameValue), "");

    public string NameValue
    {
        get => GetValue(NameValueProperty);
        set => SetValue(NameValueProperty, value);
    }

    public CircleEditorDialog() => InitializeComponent();

    /// <summary>弹出社团编辑器，返回非 null 表示用户确认（含填好的名字），null = 取消。</summary>
    public static async Task<string?> ShowAsync(string title = "添加社团", string? initialName = null)
    {
        var view = new CircleEditorDialog { NameValue = initialName ?? "" };

        var isEdit = initialName is not null;
        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(title, isEdit),
            Content = view,
            PrimaryButtonText = isEdit ? "保存" : "添加",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(view.NameValue),
        };

        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == NameValueProperty)
                dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(view.NameValue);
        };

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;
        var trimmed = view.NameValue?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
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
                new TextBlock { Text = isEdit ? "✏️" : "✚", FontSize = 22,
                                Foreground = iconBrush, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center },
            }
        };
    }
}
