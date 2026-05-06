using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using Serilog;
using AvaloniaPath = Avalonia.Controls.Shapes.Path;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 系统托盘服务。负责：
/// - 创建 TrayIcon + 动态菜单（"打开主窗 / 任务状态 / 退出"）
/// - 1s 轮询 TaskProgressService 更新菜单文案 + 图标颜色
/// - 拦截"主窗关闭"——根据 <see cref="DesktopPreferences.CloseAction"/> 决定隐藏或真退出
/// - "退出"通过 <see cref="IsExitRequested"/> 标记跳过 close-to-tray 拦截
/// 跨平台：Avalonia 内置 TrayIcon 在 Win11 / Win10 / macOS / Ubuntu (大多数桌面环境) 都能用。
/// Linux 上 libappindicator 兼容性脆弱——失败时静默不显示托盘，应用仍能用。
/// </summary>
public class TrayService : IDisposable
{
    private readonly TaskProgressService _progressService;
    private readonly DesktopPreferences _preferences;

    private TrayIcon? _trayIcon;
    private NativeMenu? _menu;
    private NativeMenuItem? _statusHeader;
    private NativeMenuItem? _runningItem;
    private NativeMenuItem? _failedItem;
    private DispatcherTimer? _pollTimer;
    private TrayState _currentState = TrayState.Idle;
    private bool _disposed;

    /// <summary>
    /// 用户从托盘"退出"或快捷键退出时置 true，让 MainWindow.Closing 跳过 close-to-tray 拦截。
    /// </summary>
    public bool IsExitRequested { get; private set; }

    public TrayService(TaskProgressService progressService, DesktopPreferences preferences)
    {
        _progressService = progressService;
        _preferences = preferences;
    }

    /// <summary>启动 TrayIcon + 轮询。在 App.OnFrameworkInitializationCompleted 调用一次。</summary>
    public void Initialize()
    {
        if (_disposed || _trayIcon is not null) return;
        if (Application.Current is null) return;

        try
        {
            _menu = BuildMenu();
            _trayIcon = new TrayIcon
            {
                ToolTipText = "NineKgTools",
                Icon = RenderTrayIcon(TrayState.Idle),
                Menu = _menu,
                IsVisible = true,
            };
            _trayIcon.Clicked += (_, _) => ShowMainWindow();

            // Avalonia 11 通过 attached 集合把 TrayIcon 挂到 Application 上
            var icons = new TrayIcons { _trayIcon };
            TrayIcon.SetIcons(Application.Current, icons);

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pollTimer.Tick += (_, _) => RefreshStatus();
            _pollTimer.Start();

            Log.Information("TrayService 已初始化");
        }
        catch (Exception ex)
        {
            // 某些 Linux 桌面环境上 TrayIcon 不可用——静默失败，主窗仍能用
            Log.Warning(ex, "TrayService 初始化失败（平台可能不支持托盘），跳过");
            _trayIcon = null;
        }
    }

    /// <summary>用户从托盘"退出"——置标记 + 触发 lifetime.Shutdown()。</summary>
    public void RequestExit()
    {
        IsExitRequested = true;
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
            {
                lifetime.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Tray RequestExit 失败");
        }
    }

    /// <summary>把主窗显示出来 + 抢前台。从托盘单击 / 菜单"打开主窗"调用。</summary>
    public void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime
            || lifetime.MainWindow is null) return;

        var main = lifetime.MainWindow;
        try
        {
            if (!main.IsVisible) main.Show();
            if (main.WindowState == Avalonia.Controls.WindowState.Minimized)
                main.WindowState = Avalonia.Controls.WindowState.Normal;
            main.Activate();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ShowMainWindow 失败");
        }
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();

        _statusHeader = new NativeMenuItem("◆ NineKgTools (空闲)") { IsEnabled = false };
        menu.Add(_statusHeader);
        menu.Add(new NativeMenuItemSeparator());

        var openItem = new NativeMenuItem("打开主窗");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Add(openItem);

        menu.Add(new NativeMenuItemSeparator());

        _runningItem = new NativeMenuItem("0 个任务运行中") { IsEnabled = false };
        menu.Add(_runningItem);

        _failedItem = new NativeMenuItem("0 个任务失败") { IsEnabled = false };
        menu.Add(_failedItem);

        menu.Add(new NativeMenuItemSeparator());

        var quitItem = new NativeMenuItem("退出 NineKgTools");
        quitItem.Click += (_, _) => RequestExit();
        menu.Add(quitItem);

        return menu;
    }

    private void RefreshStatus()
    {
        try
        {
            var all = _progressService.GetAllRootTasks().ToList();
            var running = all.Count(t => t.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying);
            var failed = all.Count(t => t.Status is TaskExecutionStatus.Failed
                or TaskExecutionStatus.Timeout);

            var newState = failed > 0 ? TrayState.HasFailures
                          : running > 0 ? TrayState.Running
                          : TrayState.Idle;

            // 状态变了才重渲染图标——renderTargetBitmap 不是免费的
            if (newState != _currentState)
            {
                _currentState = newState;
                if (_trayIcon != null)
                {
                    _trayIcon.Icon = RenderTrayIcon(newState);
                }
            }

            if (_statusHeader != null)
            {
                _statusHeader.Header = newState switch
                {
                    TrayState.Idle => "◆ NineKgTools (空闲)",
                    TrayState.Running => $"◆ NineKgTools (运行中: {running})",
                    TrayState.HasFailures => $"⚠ NineKgTools ({failed} 个失败)",
                    _ => "◆ NineKgTools",
                };
            }

            if (_runningItem != null)
                _runningItem.Header = running > 0 ? $"▶ {running} 个任务运行中" : "0 个任务运行中";
            if (_failedItem != null)
                _failedItem.Header = failed > 0 ? $"✕ {failed} 个任务失败" : "0 个任务失败";

            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = newState switch
                {
                    TrayState.Running => $"NineKgTools · {running} 个任务运行中",
                    TrayState.HasFailures => $"NineKgTools · {failed} 个任务失败",
                    _ => "NineKgTools",
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TrayService 状态刷新失败");
        }
    }

    /// <summary>
    /// 把 IconLibrary 几何图形渲染成 32×32 PNG，按状态着色。
    /// Avalonia RenderTargetBitmap 在所有平台都可用，无需 ICO 文件资产。
    /// </summary>
    private WindowIcon RenderTrayIcon(TrayState state)
    {
        var brushKey = state switch
        {
            TrayState.HasFailures => "SystemFillColorCriticalBrush",
            TrayState.Running => "SystemFillColorAttentionBrush",
            _ => "AccentFillColorDefaultBrush",
        };
        IBrush brush = Brushes.SteelBlue;
        if (Application.Current?.Resources.TryGetResource(
                brushKey, Application.Current.ActualThemeVariant, out var b) == true && b is IBrush br)
        {
            brush = br;
        }

        Geometry geometry = StreamGeometry.Parse(
            "M9,3V18H12V3H9M14,5V18H17V5H14M5,5V18H7V5H5M3,20V22H21V20H3Z");
        if (Application.Current?.Resources.TryGetResource(
                "IconLibrary", Application.Current.ActualThemeVariant, out var g) == true && g is Geometry gg)
        {
            geometry = gg;
        }

        const int size = 32;
        var path = new AvaloniaPath
        {
            Data = geometry,
            Fill = brush,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        path.Measure(new Size(size, size));
        path.Arrange(new Rect(0, 0, size, size));

        var rtb = new RenderTargetBitmap(new PixelSize(size, size));
        rtb.Render(path);

        using var ms = new MemoryStream();
        rtb.Save(ms);
        ms.Position = 0;
        return new WindowIcon(ms);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _pollTimer?.Stop();
            _pollTimer = null;
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = false;
            }
        }
        catch { /* 退出路径，不阻塞 */ }
    }
}

public enum TrayState { Idle, Running, HasFailures, Paused }
