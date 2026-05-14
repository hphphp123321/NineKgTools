using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.Views.Pages;

public partial class HomePage : UserControl
{
    /// <summary>Recent card 单元宽度 = 卡 112 + spacing 12 = 124px/张。
    /// SizeChanged 时按 RecentClip 容器宽度算可放下几张，更新 VM.VisibleRecentCount。</summary>
    private const double RecentCardCellWidth = 124;

    public HomePage()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttached;
    }

    /// <summary>
    /// AttachedToVisualTree 时触发：
    /// 1) 5 个 section 按 70ms 错峰 stagger 入场（home-section style 控制初始态 + transition）
    /// 2) 绑定 RecentClip.SizeChanged 计算 VisibleRecentCount，实现 responsive overflow=clip
    ///    （能放几张显示几张，多了不渲染、不出现横滚条）
    ///
    /// stacked bar grow 由 VM 派生 XxxBarWidth + DoubleTransition 自动触发。
    /// Hero 数字 count-up 由 VM 内 DispatcherTimer 驱动 DisplayMediaCount，与 code-behind 无关。
    /// </summary>
    private void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var sections = new (Control? Ctrl, int DelayMs)[]
        {
            (HeroSection, 0),
            (RecentSection, 70),
            (OutlineSection, 140),
            (StatusSection, 210),
            (NavChipSection, 280),
        };

        foreach (var (ctrl, delay) in sections)
        {
            if (ctrl is null) continue;
            DispatcherTimer.RunOnce(
                () => ctrl.Classes.Add("shown"),
                TimeSpan.FromMilliseconds(delay));
        }

        // Recent 响应式：RecentClip 容器宽度变化时计算可放下几张并更新 VM
        if (RecentClip is not null)
        {
            RecentClip.SizeChanged += OnRecentClipSizeChanged;
            // 首次手动触发一次（AttachedToVisualTree 时 Bounds 可能已就绪）
            UpdateVisibleRecentCount(RecentClip.Bounds.Width);
        }
    }

    private void OnRecentClipSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateVisibleRecentCount(e.NewSize.Width);
    }

    private void UpdateVisibleRecentCount(double width)
    {
        if (DataContext is not HomeViewModel vm) return;
        if (width <= 0) return;
        // Math.Floor 保证最后一张完整可见而非半张被裁
        var count = (int)Math.Floor((width + 12) / RecentCardCellWidth);
        // 最少 1 张，最多 8 张（VM 实际上限 = RecentMedias.Count，Take 自动 cap）
        count = Math.Clamp(count, 1, 8);
        if (vm.VisibleRecentCount != count)
            vm.VisibleRecentCount = count;
    }
}
