using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.Views.Dialogs;

/// <summary>
/// 创作者添加对话框。与 Web `CreatorsPage` "添加创作者" dialog 等价：
/// Name + 7 个 CreatorType 多选 ToggleButton chip（可全不选）。
/// 不做重名验证——CreatorService.CreateCreatorAsync 同名允许（按 Id 区分）。
/// </summary>
public partial class CreatorEditorDialog : UserControl
{
    public static readonly StyledProperty<string> NameValueProperty =
        AvaloniaProperty.Register<CreatorEditorDialog, string>(nameof(NameValue), "");

    public static readonly StyledProperty<ObservableCollection<CreatorTypeToggleVm>> TypeTogglesProperty =
        AvaloniaProperty.Register<CreatorEditorDialog, ObservableCollection<CreatorTypeToggleVm>>(
            nameof(TypeToggles), new ObservableCollection<CreatorTypeToggleVm>());

    public string NameValue
    {
        get => GetValue(NameValueProperty);
        set => SetValue(NameValueProperty, value);
    }

    public ObservableCollection<CreatorTypeToggleVm> TypeToggles
    {
        get => GetValue(TypeTogglesProperty);
        set => SetValue(TypeTogglesProperty, value);
    }

    public CreatorEditorDialog() => InitializeComponent();

    public record Result(string Name, IReadOnlyList<CreatorType> Types);

    /// <summary>
    /// 弹出创作者编辑器。返回非 null 表示用户确认；types 可能为空列表（Web 也允许全不选）。
    /// </summary>
    public static async Task<Result?> ShowAsync(
        string title = "添加创作者",
        string? initialName = null,
        IReadOnlyCollection<CreatorType>? initialTypes = null)
    {
        var selected = initialTypes?.ToHashSet() ?? new HashSet<CreatorType>();
        var toggles = new ObservableCollection<CreatorTypeToggleVm>();
        foreach (var type in new[]
                 {
                     CreatorType.Author, CreatorType.Illustrator, CreatorType.Musician,
                     CreatorType.ScreenWriter, CreatorType.VoiceActor,
                     CreatorType.Director, CreatorType.Actor,
                 })
        {
            toggles.Add(new CreatorTypeToggleVm(type, MapType(type), selected.Contains(type)));
        }

        var view = new CreatorEditorDialog
        {
            NameValue = initialName ?? "",
            TypeToggles = toggles,
        };

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

        var dlgResult = await dialog.ShowAsync();
        if (dlgResult != FAContentDialogResult.Primary) return null;
        var trimmed = view.NameValue?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;

        var chosen = view.TypeToggles.Where(t => t.IsSelected).Select(t => t.Type).ToList();
        return new Result(trimmed, chosen);
    }

    private static string MapType(CreatorType t) => t switch
    {
        CreatorType.Author => "作者",
        CreatorType.Illustrator => "画师",
        CreatorType.Musician => "音乐",
        CreatorType.ScreenWriter => "编剧",
        CreatorType.VoiceActor => "声优",
        CreatorType.Director => "导演",
        CreatorType.Actor => "演员",
        _ => t.ToString()
    };

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
                new TextBlock { Text = isEdit ? "✏️" : "✚", FontSize = 22,
                                Foreground = iconBrush, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center },
            }
        };
    }
}
