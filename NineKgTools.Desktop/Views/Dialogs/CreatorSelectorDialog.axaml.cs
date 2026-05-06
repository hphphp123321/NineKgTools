using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class CreatorSelectorDialog : UserControl
{
    private const int MaxRenderedCreators = 50;
    private const int MinSearchLength = 2;

    /// <summary>搜索防抖窗口——防止 IME 组字过程中的逐字符触发</summary>
    private const int DebounceMs = 300;

    public CreatorSelectorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出创作者选择器。返回 null = 取消；返回 List = 用户确认（多选可能为空 = 清空）。
    /// 不做"新建创作者"面板（§4.x P2 范围，本对话框只选已有）。
    /// </summary>
    public static async Task<List<Creator>?> ShowAsync(
        IReadOnlyList<Creator> initialSelected,
        bool allowMultiSelect,
        CreatorType? initialFilterType,
        CreatorService creatorService)
    {
        ArgumentNullException.ThrowIfNull(creatorService);

        var ctx = new CreatorSelectorDialogContext(allowMultiSelect);
        ctx.Initialize(initialSelected);
        if (initialFilterType.HasValue)
        {
            var match = ctx.TypeOptions.FirstOrDefault(o => o.Value == initialFilterType.Value);
            if (match is not null) ctx.SelectedType = match;
        }

        var view = new CreatorSelectorDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(allowMultiSelect),
            Content = view,
            PrimaryButtonText = ctx.ConfirmText,
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = ctx.CanSubmit,
        };

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

        // 防抖加载——SearchText / SelectedType 改变时重启 timer
        DispatcherTimer? debounceTimer = null;
        void ScheduleLoad()
        {
            debounceTimer?.Stop();
            debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
            debounceTimer.Tick += async (_, _) =>
            {
                debounceTimer.Stop();
                await LoadAsync(ctx, creatorService);
            };
            debounceTimer.Start();
        }

        ctx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ctx.SearchText) or nameof(ctx.SelectedType))
                ScheduleLoad();
        };

        // 首次加载（无防抖）
        _ = LoadAsync(ctx, creatorService);

        var result = await dialog.ShowAsync();
        debounceTimer?.Stop();

        if (result != FAContentDialogResult.Primary) return null;
        return ctx.CollectSelected();
    }

    private static async Task LoadAsync(CreatorSelectorDialogContext ctx, CreatorService creatorService)
    {
        var trimmed = ctx.SearchText?.Trim() ?? "";
        // 少于 MinSearchLength 字符 + 非空时跳过——避免 IME 组字阶段的查询风暴
        if (trimmed.Length > 0 && trimmed.Length < MinSearchLength) return;

        ctx.IsLoading = true;
        try
        {
            var results = trimmed.Length == 0
                ? await creatorService.GetAllCreatorsAsync()
                : await creatorService.SearchCreatorsByNameAsync(trimmed, MaxRenderedCreators);

            // 类型筛选——服务端无组合查询，客户端兜底
            var filterType = ctx.SelectedType?.Value;
            if (filterType.HasValue)
            {
                results = results.Where(c => c.Types.Contains(filterType.Value)).ToList();
            }

            var truncated = results.Count > MaxRenderedCreators;
            var view = truncated ? results.Take(MaxRenderedCreators).ToList() : results;

            await Dispatcher.UIThread.InvokeAsync(() => ctx.ApplyResults(view, truncated));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CreatorSelectorDialog 加载失败 SearchTerm={Q}", trimmed);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => ctx.IsLoading = false);
        }
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
                    Text = "👤",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = allowMultiSelect ? "选择创作者（多选）" : "选择创作者",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
