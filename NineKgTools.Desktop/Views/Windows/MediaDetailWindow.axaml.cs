using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.Views.Windows;

/// <summary>
/// 媒体详情独立窗口（power-user 选项，由 in-page [↗ 在新窗口] 按钮触发）。
///
/// **共享 UI**：MediaDetailContent UserControl 同时被 Views/Pages/MediaDetailPage 和本窗口使用。
/// 差异在 host：本窗口提供 OS chrome + 图钉（VM.IsIndependentWindow=true 才可见），
/// 内嵌页提供顶部 nav bar（VM.IsEmbeddedPage=true 才用）。
///
/// **VM Mode 注入**：DataContext 赋值后立刻把 VM.Mode 设为 IndependentWindow，让 UserControl
/// 内的图钉等元素显示出来。
///
/// **Topmost 双向同步**：VM.IsTopmost 是 ObservableProperty —— 用户点 UserControl 内的图钉
/// 修改 VM.IsTopmost；本 code-behind 监听该 PropertyChanged 把变化同步到 Window.Topmost。
/// 反向：用户从外部（任务管理器 / Ctrl+T 快捷键等）改 Window.Topmost，监听 TopmostProperty 写回 VM。
/// 不能直接用 $parent[Window].Topmost 绑定——in-page 模式 UserControl 父级是 MainWindow 不是本窗口。
/// </summary>
public partial class MediaDetailWindow : Window
{
    public MediaDetailWindow()
    {
        InitializeComponent();
        // 同类型窗口共享一份位置记忆 — 不同 mediaId 用同一 key="media"，避免 N 个 media 各占一份冗余
        this.EnableChildWindowFeatures("media");

        DataContextChanged += OnDataContextChanged;
        // Window.Topmost 反向同步到 VM（外部触发 Topmost 变化时）
        PropertyChanged += OnWindowPropertyChanged;
    }

    private MediaDetailViewModel? _subscribedVm;

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            _subscribedVm.DeleteCompleted -= OnDeleteCompleted;
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
            _subscribedVm = null;
        }
        if (DataContext is MediaDetailViewModel vm)
        {
            vm.DeleteCompleted += OnDeleteCompleted;
            vm.PropertyChanged += OnVmPropertyChanged;
            // 标记为独立窗模式——让 UserControl 内的图钉等元素可见
            vm.Mode = MediaDetailMode.IndependentWindow;
            // 初始同步：Window 当前 Topmost → VM
            vm.IsTopmost = Topmost;
            _subscribedVm = vm;
        }
    }

    /// <summary>VM.IsTopmost 变化 → Window.Topmost</summary>
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaDetailViewModel.IsTopmost) && _subscribedVm is not null)
        {
            if (Topmost != _subscribedVm.IsTopmost)
                Topmost = _subscribedVm.IsTopmost;
        }
    }

    /// <summary>Window.Topmost 变化 → VM.IsTopmost（避免循环：仅在值真的不同时写）</summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopmostProperty && _subscribedVm is not null)
        {
            if (_subscribedVm.IsTopmost != Topmost)
                _subscribedVm.IsTopmost = Topmost;
        }
    }

    private void OnDeleteCompleted(object? sender, System.EventArgs e)
    {
        // 切回 UI 线程关窗（VM 的 Save/Delete 走 RelayCommand 已经在 UI 线程，但 InvokeAsync 兜底）
        Avalonia.Threading.Dispatcher.UIThread.Post(Close);
    }

    /// <summary>Window 关闭时显式调 VM.OnLeaveAsync 让它取消 Singleton 服务订阅
    /// （NavigationService.CanGoBackChanged / DesktopPreferences.UseGlassBackgroundChanged）。
    /// 独立窗不走 NavigationService 流程，必须由本 host 主动通知否则 Singleton 累积 handler 引用 leak VM。</summary>
    protected override void OnClosed(System.EventArgs e)
    {
        if (_subscribedVm is not null)
        {
            try { _ = _subscribedVm.OnLeaveAsync(); }
            catch (System.Exception ex) { Serilog.Log.Warning(ex, "MediaDetailWindow.OnClosed: VM.OnLeaveAsync 失败"); }
        }
        base.OnClosed(e);
    }
}
