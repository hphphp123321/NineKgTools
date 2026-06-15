using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.Views.Dialogs;

/// <summary>
/// 通用单行输入对话框。返回 trim 后的字符串；取消 / 输入空 → null。
///
/// 用例：收藏夹重命名 / 创作者添加 / 任何"给我个名字"场景。比起每个场景各做一个
/// XxxEditorDialog，这里复用——参数化 title / message / initialValue / placeholder 即可。
/// </summary>
public partial class InputDialog : UserControl
{
    public static readonly StyledProperty<string> InputValueProperty =
        AvaloniaProperty.Register<InputDialog, string>(nameof(InputValue), "");

    public static readonly StyledProperty<string> MessageTextProperty =
        AvaloniaProperty.Register<InputDialog, string>(nameof(MessageText), "");

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<InputDialog, string>(nameof(Placeholder), "");

    public static readonly StyledProperty<string> HelperTextProperty =
        AvaloniaProperty.Register<InputDialog, string>(nameof(HelperText), "");

    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<InputDialog, int>(nameof(MaxLength), 200);

    public string InputValue
    {
        get => GetValue(InputValueProperty);
        set => SetValue(InputValueProperty, value);
    }

    public string MessageText
    {
        get => GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string HelperText
    {
        get => GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
    }

    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public bool HasMessage => !string.IsNullOrEmpty(MessageText);
    public bool HasHelperText => !string.IsNullOrEmpty(HelperText);

    public InputDialog() => InitializeComponent();

    /// <summary>
    /// 弹出输入对话框。空字符串 / 取消 → null；与 initialValue 相同也返回（调用方决定要不要忽略）。
    /// </summary>
    public static async Task<string?> ShowAsync(
        string title,
        string? message = null,
        string? initialValue = null,
        string? placeholder = null,
        string? helperText = null,
        string? confirmText = null,
        int maxLength = 200,
        Func<string, bool>? validate = null)
    {
        var view = new InputDialog
        {
            InputValue = initialValue ?? "",
            MessageText = message ?? "",
            Placeholder = placeholder ?? "",
            HelperText = helperText ?? "",
            MaxLength = maxLength,
        };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(title, isEdit: !string.IsNullOrEmpty(initialValue)),
            Content = view,
            PrimaryButtonText = confirmText ?? "确定",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        // 表单校验：默认非空 trim 即可；调用方传 validate 可加更严约束（如不能与原值相同）
        bool IsValid()
        {
            var v = view.InputValue?.Trim() ?? "";
            if (v.Length == 0) return false;
            return validate?.Invoke(v) ?? true;
        }
        void Recompute() => dialog.IsPrimaryButtonEnabled = IsValid();
        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == InputValueProperty) Recompute();
        };
        Recompute();

        // 自动聚焦输入框
        view.AttachedToVisualTree += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (view.FindControl<TextBox>("ValueBox") is { } box)
                {
                    box.Focus();
                    box.SelectAll();
                }
            }, DispatcherPriority.Background);
        };

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;

        var trimmed = view.InputValue?.Trim() ?? "";
        if (trimmed.Length == 0) return null;
        return trimmed;
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
