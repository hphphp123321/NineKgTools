using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;

namespace NineKgTools.Desktop.Views.Dialogs;

/// <summary>
/// 标签映射编辑器（添加 / 编辑）。与 Web 端 TagMappingEditorDialog 等价：
/// 源标签名称 + 目标标签（嵌套 TagSelectorDialog 单选）+ 描述。
/// Priority 字段不暴露（默认 100，留位扩展），IsActive 默认 true。
/// </summary>
public partial class TagMappingEditorDialog : UserControl
{
    public static readonly StyledProperty<string> SourceNameValueProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(SourceNameValue), "");

    public static readonly StyledProperty<string> DescriptionValueProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(DescriptionValue), "");

    public static readonly StyledProperty<Tag?> SelectedTargetTagProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, Tag?>(nameof(SelectedTargetTag));

    public static readonly StyledProperty<string> TargetTagDisplayProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(TargetTagDisplay), "尚未选择");

    public static readonly StyledProperty<string> TargetTagSubTextProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(TargetTagSubText), "");

    public static readonly StyledProperty<bool> HasTargetTagSubTextProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, bool>(nameof(HasTargetTagSubText));

    public static readonly StyledProperty<string> SelectButtonTextProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(SelectButtonText), "选择");

    public static readonly StyledProperty<string> ErrorMessageProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, string>(nameof(ErrorMessage), "");

    public static readonly StyledProperty<bool> HasErrorMessageProperty =
        AvaloniaProperty.Register<TagMappingEditorDialog, bool>(nameof(HasErrorMessage));

    public string SourceNameValue
    {
        get => GetValue(SourceNameValueProperty);
        set => SetValue(SourceNameValueProperty, value);
    }

    public string DescriptionValue
    {
        get => GetValue(DescriptionValueProperty);
        set => SetValue(DescriptionValueProperty, value);
    }

    public Tag? SelectedTargetTag
    {
        get => GetValue(SelectedTargetTagProperty);
        set => SetValue(SelectedTargetTagProperty, value);
    }

    public string TargetTagDisplay
    {
        get => GetValue(TargetTagDisplayProperty);
        set => SetValue(TargetTagDisplayProperty, value);
    }

    public string TargetTagSubText
    {
        get => GetValue(TargetTagSubTextProperty);
        set => SetValue(TargetTagSubTextProperty, value);
    }

    public bool HasTargetTagSubText
    {
        get => GetValue(HasTargetTagSubTextProperty);
        set => SetValue(HasTargetTagSubTextProperty, value);
    }

    public string SelectButtonText
    {
        get => GetValue(SelectButtonTextProperty);
        set => SetValue(SelectButtonTextProperty, value);
    }

    public string ErrorMessage
    {
        get => GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    public bool HasErrorMessage
    {
        get => GetValue(HasErrorMessageProperty);
        set => SetValue(HasErrorMessageProperty, value);
    }

    /// <summary>TagService 注入——OnSelectTargetTag 弹 TagSelectorDialog 需要。</summary>
    private TagService? _tagService;

    public TagMappingEditorDialog() => InitializeComponent();

    /// <summary>"选择目标标签"按钮 click → 弹 TagSelectorDialog 单选 → 写回 SelectedTargetTag。</summary>
    private async void OnSelectTargetTag(object? sender, RoutedEventArgs e)
    {
        if (_tagService is null) return;
        var initial = SelectedTargetTag is not null
            ? new List<Tag> { SelectedTargetTag }
            : new List<Tag>();
        var result = await TagSelectorDialog.ShowAsync(initial, allowMultiSelect: false, _tagService);
        if (result is { Count: > 0 })
        {
            SelectedTargetTag = result[0];
        }
    }

    /// <summary>当用户编辑源名 / 目标已选 → 调用方传入的"验重 + 校验"逻辑可能改 ErrorMessage</summary>
    private void UpdateTargetTagDisplay()
    {
        var tag = SelectedTargetTag;
        if (tag is null)
        {
            TargetTagDisplay = "尚未选择";
            TargetTagSubText = "";
            HasTargetTagSubText = false;
            SelectButtonText = "选择";
        }
        else
        {
            TargetTagDisplay = tag.Name;
            TargetTagSubText = tag.TopTag is not null ? $"所属分组：{tag.TopTag.Name}" : "";
            HasTargetTagSubText = !string.IsNullOrEmpty(TargetTagSubText);
            SelectButtonText = "更换";
        }
    }

    public record Result(string SourceName, Tag TargetTag, string? Description);

    /// <summary>
    /// 弹出添加 / 编辑映射对话框。返回非 null 表示用户确认。
    /// <paramref name="existingSourceNames"/> 用于实时重名校验（编辑时排除自身名）。
    /// </summary>
    public static async Task<Result?> ShowAsync(
        TagService tagService,
        IReadOnlyList<string> existingSourceNames,
        string? initialSourceName = null,
        Tag? initialTargetTag = null,
        string? initialDescription = null)
    {
        ArgumentNullException.ThrowIfNull(tagService);

        var view = new TagMappingEditorDialog
        {
            _tagService = tagService,
            SourceNameValue = initialSourceName ?? "",
            SelectedTargetTag = initialTargetTag,
            DescriptionValue = initialDescription ?? "",
        };
        view.UpdateTargetTagDisplay();

        var isEdit = initialSourceName is not null;
        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(isEdit ? "编辑标签映射" : "添加标签映射"),
            Content = view,
            PrimaryButtonText = isEdit ? "保存" : "添加映射",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        // 校验：源名非空 + 目标已选 + 不重名（忽略大小写、Trim；编辑模式排除自身原名）
        void Recompute()
        {
            view.UpdateTargetTagDisplay();
            var name = view.SourceNameValue?.Trim() ?? "";
            var hasName = name.Length > 0;
            var hasTarget = view.SelectedTargetTag is not null;

            if (hasName)
            {
                var isDup = existingSourceNames.Any(s =>
                    !string.IsNullOrEmpty(s)
                    && (!isEdit || !s.Equals(initialSourceName, StringComparison.OrdinalIgnoreCase))
                    && s.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (isDup)
                {
                    view.ErrorMessage = "该源名已存在映射";
                    view.HasErrorMessage = true;
                    dialog.IsPrimaryButtonEnabled = false;
                    return;
                }
            }

            view.ErrorMessage = "";
            view.HasErrorMessage = false;
            dialog.IsPrimaryButtonEnabled = hasName && hasTarget;
        }

        view.PropertyChanged += (_, e) =>
        {
            if (e.Property == SourceNameValueProperty || e.Property == SelectedTargetTagProperty)
                Recompute();
        };
        Recompute();

        var dlgResult = await dialog.ShowAsync();
        if (dlgResult != FAContentDialogResult.Primary) return null;

        var sourceName = view.SourceNameValue?.Trim();
        var targetTag = view.SelectedTargetTag;
        if (string.IsNullOrEmpty(sourceName) || targetTag is null) return null;

        var desc = string.IsNullOrWhiteSpace(view.DescriptionValue) ? null : view.DescriptionValue.Trim();
        return new Result(sourceName, targetTag, desc);
    }

    private static Control BuildTitleVisual(string title)
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
                    Text = "🔗",
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
