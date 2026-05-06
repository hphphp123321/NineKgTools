using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class CreatorMergeDialog : UserControl
{
    public static readonly StyledProperty<string> SourceNameProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, string>(nameof(SourceName), "");

    public static readonly StyledProperty<string> SourceMediaCountTextProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, string>(nameof(SourceMediaCountText), "");

    public static readonly StyledProperty<ObservableCollection<Creator>> AllTargetsProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, ObservableCollection<Creator>>(
            nameof(AllTargets), new ObservableCollection<Creator>());

    public static readonly StyledProperty<object?> SelectedTargetProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, object?>(nameof(SelectedTarget));

    public static readonly StyledProperty<bool> ShowPreviewProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, bool>(nameof(ShowPreview));

    public static readonly StyledProperty<string> PreviewTextProperty =
        AvaloniaProperty.Register<CreatorMergeDialog, string>(nameof(PreviewText), "");

    public string SourceName { get => GetValue(SourceNameProperty); set => SetValue(SourceNameProperty, value); }
    public string SourceMediaCountText { get => GetValue(SourceMediaCountTextProperty); set => SetValue(SourceMediaCountTextProperty, value); }
    public ObservableCollection<Creator> AllTargets { get => GetValue(AllTargetsProperty); set => SetValue(AllTargetsProperty, value); }
    public object? SelectedTarget { get => GetValue(SelectedTargetProperty); set => SetValue(SelectedTargetProperty, value); }
    public bool ShowPreview { get => GetValue(ShowPreviewProperty); set => SetValue(ShowPreviewProperty, value); }
    public string PreviewText { get => GetValue(PreviewTextProperty); set => SetValue(PreviewTextProperty, value); }

    public CreatorMergeDialog() => InitializeComponent();

    /// <summary>
    /// 弹出合并创作者对话框；返回选中的目标 Creator（确认合并 = 非 null），null = 取消。
    /// </summary>
    public static async Task<Creator?> ShowAsync(
        Creator source,
        IReadOnlyList<Creator> allCreators)
    {
        var sourceMediaCount = source.Medias?.Count ?? 0;

        // 候选 = 全部 - 自己
        var candidates = new ObservableCollection<Creator>(
            allCreators.Where(c => c.Id != source.Id).OrderBy(c => c.Name));

        var view = new CreatorMergeDialog
        {
            SourceName = source.Name,
            SourceMediaCountText = sourceMediaCount > 0
                ? $"共 {sourceMediaCount} 件作品 · {source.AliasNames.Count} 个别名"
                : "（暂无关联作品）",
            AllTargets = candidates,
        };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = "确认合并",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };

        view.PropertyChanged += (_, e) =>
        {
            if (e.Property != SelectedTargetProperty) return;
            var sel = view.SelectedTarget as Creator;
            dialog.IsPrimaryButtonEnabled = sel is not null;
            view.ShowPreview = sel is not null;
            if (sel is not null)
            {
                var targetCount = sel.Medias?.Count ?? 0;
                view.PreviewText =
                    $"{sourceMediaCount} 件作品将迁移到「{sel.Name}」（合并后该创作者共 ≤ {sourceMediaCount + targetCount} 件，重叠去重）。" +
                    $"创作者「{source.Name}」会被删除。";
            }
        };

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;
        return view.SelectedTarget as Creator;
    }

    private static Control BuildTitleVisual()
    {
        IBrush iconBrush = Brushes.Crimson;
        if (Application.Current?.Resources.TryGetResource(
                "SystemFillColorCriticalBrush", Application.Current.ActualThemeVariant, out var b) == true
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
                    Text = "⚠",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "合并创作者",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
