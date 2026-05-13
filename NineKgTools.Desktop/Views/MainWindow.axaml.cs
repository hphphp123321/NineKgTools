using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _dragHoverTimer;
    private bool _dragHasFiles;

    /// <summary>
    /// 反向同步 NavView.SelectedItem 时屏蔽 SelectionChanged 处理，避免：
    /// HomePage 命令 NavigateAsync → CurrentPageChanged → set SelectedItem
    /// → SelectionChanged 再触发一次 NavigateAsync → 创建第二个 VM 实例（页面闪烁）。
    /// </summary>
    private bool _suppressSelectionChanged;

    public MainWindow()
    {
        InitializeComponent();

        // 启用拖拽接收
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // 主窗位置记忆（key="main"）
        try
        {
            Program.Services?.GetService<WindowStateService>()?.Attach(this, "main");
        }
        catch (Exception ex) { Log.Warning(ex, "MainWindow WindowStateService.Attach 失败"); }

        // 全局快捷键：Ctrl+1..9 跳到对应导航项；Ctrl+W 不响应（避免误关主窗）
        AddHandler(KeyDownEvent, OnGlobalKeyDown, handledEventsToo: false);

        // 订阅 NavigationService.CurrentPageChanged，把代码触发的导航（例如 HomePage 的
        // GoToMediaLibrary 命令）反向同步到侧栏 SelectedItem 上——否则用户在首页点击
        // "进入媒体库"后，左栏依旧高亮"首页"。
        try
        {
            var nav = Program.Services?.GetService<NavigationService>();
            if (nav is not null)
                nav.CurrentPageChanged += OnNavigationCurrentPageChanged;
        }
        catch (Exception ex) { Log.Warning(ex, "MainWindow 订阅 NavigationService.CurrentPageChanged 失败"); }
    }

    private void OnNavigationCurrentPageChanged(object? sender, PageViewModelBase? page)
    {
        if (page is null) return;
        try
        {
            object? target;
            if (page is SettingsViewModel)
            {
                // FluentAvalonia FANavigationView 把"设置"作为单独的 SettingsItem 暴露
                target = NavView.SettingsItem;
            }
            else
            {
                var typeName = page.GetType().Name;
                target = NavView.MenuItems.OfType<FANavigationViewItem>()
                    .FirstOrDefault(it => (it.Tag as string) == typeName);
            }

            if (target is null) return;
            if (ReferenceEquals(NavView.SelectedItem, target)) return;

            _suppressSelectionChanged = true;
            try { NavView.SelectedItem = target; }
            finally { _suppressSelectionChanged = false; }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "反向同步 NavView.SelectedItem 失败：{Type}", page.GetType().Name);
        }
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // Backspace 后退（与浏览器一致）：仅在非输入控件聚焦时触发；
        // TextBox 内删字按 Backspace 是合法操作，不抢占。
        if (e.KeyModifiers == KeyModifiers.None && e.Key == Key.Back)
        {
            if (e.Source is TextBox) return;
            try
            {
                var nav = Program.Services?.GetService<NavigationService>();
                if (nav?.CanGoBack == true)
                {
                    _ = nav.NavigateBackAsync();
                    e.Handled = true;
                }
            }
            catch (Exception ex) { Log.Warning(ex, "Backspace 后退失败"); }
            return;
        }

        if (e.KeyModifiers != KeyModifiers.Control) return;

        // Ctrl+K → 聚焦全局搜索框（§12 决策入口②）
        // 用 NavigationMethod.Tab 让 GotFocus 收到非 Unspecified 的 NavigationMethod，
        // 从而自动通过 OnSearchBoxGotFocus 的 Unspecified 过滤打开 popup
        if (e.Key == Key.K)
        {
            try
            {
                GlobalSearchBox.Focus(NavigationMethod.Tab);
                GlobalSearchBox.SelectAll();
                e.Handled = true;
            }
            catch (Exception ex) { Log.Warning(ex, "Ctrl+K 聚焦搜索框失败"); }
            return;
        }

        // Ctrl + 1..9 → NavigationView 第 N 个 MenuItem
        int? targetIndex = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            _ => null,
        };

        if (targetIndex is not int idx) return;
        try
        {
            var items = NavView.MenuItems.OfType<FANavigationViewItem>().ToList();
            if (idx < items.Count)
            {
                NavView.SelectedItem = items[idx];
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ctrl+{Idx} 跳转失败", idx + 1);
        }
    }

    /// <summary>SearchBox 聚焦时打开 Flyout 预览。
    /// **NavigationMethod 关键过滤**：Avalonia 12 在 Window 启动时会把焦点自动设到第一个可 focus 元素
    /// （NavigationMethod=Unspecified），跳过它避免应用启动就弹 popup。
    /// 仅 Pointer / Tab / Directional（用户主动）才开 popup。Ctrl+K 走 Focus(NavigationMethod.Tab) 自然触发。</summary>
    private void OnSearchBoxGotFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Unspecified) return;
        if (DataContext is not MainWindowViewModel vm) return;
        vm.SearchFlyoutVm.IsOpen = true;
    }

    /// <summary>每次输入都强制 Flyout 可见——保证用户在搜索框里打字时 popup 一定显示，
    /// 即使之前被 IsLightDismiss 误关也会立刻重弹。
    /// **必须**检查 IsFocused：防止 binding 初始化 / 程序外部 set Query 时把 popup 误打开。</summary>
    private void OnSearchBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not Control c || !c.IsFocused) return;
        if (DataContext is not MainWindowViewModel vm) return;
        vm.SearchFlyoutVm.IsOpen = true;
    }

    /// <summary>
    /// SearchBox 键盘路由：
    /// - ↓/↑ → 在 Flyout 内移动高亮（即便 popup 没开也尝试开）
    /// - Enter → 激活高亮项跳详情（若无高亮则跳完整结果页）
    /// - Ctrl+Enter → 跳完整结果页
    /// - Esc → 关 popup
    /// </summary>
    private async void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var flyout = vm.SearchFlyoutVm;

        try
        {
            switch (e.Key)
            {
                case Key.Down:
                    flyout.IsOpen = true;
                    flyout.MoveSelection(1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    flyout.IsOpen = true;
                    flyout.MoveSelection(-1);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    if (flyout.IsOpen)
                    {
                        flyout.IsOpen = false;
                        e.Handled = true;
                    }
                    return;
                case Key.Enter:
                    // Web 搜索框标准体验：
                    //  - 默认无高亮（用户未按 ↓） → Enter 跳完整结果页
                    //  - 用户已按 ↓/↑ 进入键盘导航态 → Enter 激活当前高亮项
                    //  - Ctrl+Enter 永远跳完整页（覆盖键盘导航态作为兜底）
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || !flyout.HasKeyboardSelection)
                    {
                        await flyout.ViewAllAsync();
                    }
                    else
                    {
                        await flyout.ActivateHighlightedAsync();
                    }
                    e.Handled = true;
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "全局搜索 KeyDown 处理失败 Key={Key}", e.Key);
        }
    }

    private async void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs args)
    {
        // 反向同步触发的 set（HomePage 命令 → CurrentPageChanged → SelectedItem 赋值）会
        // 再次触发本事件——必须吃掉，否则会重入 NavigateAsync 创建第二个 VM 实例
        if (_suppressSelectionChanged) return;
        if (DataContext is not MainWindowViewModel vm) return;

        Type? targetType = null;

        if (args.IsSettingsSelected)
        {
            targetType = typeof(SettingsViewModel);
        }
        else if (args.SelectedItem is FANavigationViewItem item && item.Tag is string typeName)
        {
            // Tag 形如 "HomeViewModel"，命名空间在 NineKgTools.Desktop.ViewModels.Pages
            targetType = Type.GetType($"NineKgTools.Desktop.ViewModels.Pages.{typeName}, NineKgTools.Desktop");
        }

        if (targetType is null) return;

        try
        {
            await vm.NavigateAsync(targetType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导航失败 → {Type}", targetType);
        }
    }

    // ============================================================
    //  拖拽接收：DragEnter 启动 200ms 防误触 timer，超时后显示 Overlay
    //  Drop 把路径列表交给 DragDropDispatcher 处理（路径分发逻辑见服务）
    // ============================================================

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // Avalonia 12: e.Data → e.DataTransfer，DataFormats.Files → DataFormat.File
        _dragHasFiles = e.DataTransfer.Contains(DataFormat.File);
        if (!_dragHasFiles) return;

        e.DragEffects = DragDropEffects.Copy;

        _dragHoverTimer?.Stop();
        _dragHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _dragHoverTimer.Tick += (_, _) =>
        {
            _dragHoverTimer?.Stop();
            _dragHoverTimer = null;
            if (_dragHasFiles && DragOverlay is { } overlay)
            {
                overlay.IsVisible = true;
                overlay.Opacity = 1;
            }
        };
        _dragHoverTimer.Start();
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _dragHoverTimer?.Stop();
        _dragHoverTimer = null;
        if (DragOverlay is { } overlay) overlay.IsVisible = false;
        _dragHasFiles = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        _dragHoverTimer?.Stop();
        _dragHoverTimer = null;
        if (DragOverlay is { } overlay) overlay.IsVisible = false;

        var paths = DragDropDispatcher.ExtractLocalPaths(e);
        if (paths.Count == 0) return;

        try
        {
            var dispatcher = Program.Services.GetRequiredService<DragDropDispatcher>();
            await dispatcher.HandleDropAsync(paths);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理拖拽失败");
        }
    }
}
