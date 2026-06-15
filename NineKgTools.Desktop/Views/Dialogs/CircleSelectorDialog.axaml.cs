using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class CircleSelectorDialog : UserControl
{
    private const int MaxRenderedCircles = 50;
    private const int MinSearchLength = 2;
    private const int DebounceMs = 300;

    public CircleSelectorDialog() => InitializeComponent();

    /// <summary>
    /// 弹出社团选择器（单选）。返回 null = 取消；返回 Circle = 用户确认选中项。
    /// 本对话框只负责"为这个 media 挑社团"，**不提供新建 / 编辑 / 删除社团入口**——那些操作请去 CirclesPage。
    /// </summary>
    public static async Task<Circle?> ShowAsync(
        Circle? initialSelected,
        CreatorService creatorService)
    {
        ArgumentNullException.ThrowIfNull(creatorService);

        var ctx = new CircleSelectorDialogContext();
        ctx.Initialize(initialSelected);

        var view = new CircleSelectorDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary,
            IsPrimaryButtonEnabled = ctx.CanSubmit,
        };

        ctx.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ctx.CanSubmit))
                dialog.IsPrimaryButtonEnabled = ctx.CanSubmit;
        };

        // 防抖加载——SearchText 改变时重启 timer
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
            if (e.PropertyName == nameof(ctx.SearchText))
                ScheduleLoad();
        };

        _ = LoadAsync(ctx, creatorService);

        var result = await dialog.ShowAsync();
        debounceTimer?.Stop();

        if (result != FAContentDialogResult.Primary) return null;
        return ctx.CollectSelected();
    }

    private static async Task LoadAsync(CircleSelectorDialogContext ctx, CreatorService creatorService)
    {
        var trimmed = ctx.SearchText?.Trim() ?? "";
        if (trimmed.Length > 0 && trimmed.Length < MinSearchLength) return;

        ctx.IsLoading = true;
        try
        {
            var results = trimmed.Length == 0
                ? await creatorService.GetAllCirclesAsync()
                : await creatorService.SearchCirclesByNameAsync(trimmed, MaxRenderedCircles);

            var truncated = results.Count > MaxRenderedCircles;
            var view = truncated ? results.Take(MaxRenderedCircles).ToList() : results;

            await Dispatcher.UIThread.InvokeAsync(() => ctx.ApplyResults(view, truncated));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CircleSelectorDialog 加载失败 SearchTerm={Q}", trimmed);
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => ctx.IsLoading = false);
        }
    }

    private static Control BuildTitleVisual()
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
                    Text = "🏢",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "选择社团",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
