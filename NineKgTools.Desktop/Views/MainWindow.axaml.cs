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
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;

        // Ctrl+K → 聚焦全局搜索框（§12 决策入口②）
        if (e.Key == Key.K)
        {
            try
            {
                GlobalSearchBox.Focus();
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

    /// <summary>SearchBox 按 Enter → 触发 MainWindowViewModel.ExecuteSearchCommand 跳到媒体库</summary>
    private void OnSearchBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not MainWindowViewModel vm) return;

        try
        {
            if (vm.ExecuteSearchCommand.CanExecute(null))
                vm.ExecuteSearchCommand.Execute(null);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "全局搜索 Enter 提交失败");
        }
    }

    private async void OnNavigationSelectionChanged(object? sender, FANavigationViewSelectionChangedEventArgs args)
    {
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
