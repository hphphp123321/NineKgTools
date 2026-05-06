using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Hosting;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.Views;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // OnExplicitShutdown：关主窗 ≠ 退应用——让 TrayService 控制真正退出时机。
            // 不开启则 OnMainWindowClose 默认会在 Hide 时也判定 lastWindowClose 退出。
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 应用启动时按 DesktopPreferences 还原主题
            var prefs = Program.Services.GetRequiredService<DesktopPreferences>();
            ApplyPersistedTheme(prefs.Theme);

            var vm = Program.Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            // 主窗显示后再触发 AfterStartup（拉起文件夹监控、定时任务），避免阻塞 UI 初始化
            window.Opened += async (_, _) =>
            {
                try
                {
                    await vm.InitializeAsync();
                    await AppBootstrap.RunAfterStartupAsync(Program.Services);

                    // Tray 必须在 MainWindow.Opened 之后初始化——某些平台（macOS）
                    // TrayIcon 注册需要 Application 已经显示过窗口
                    Program.Services.GetService<TrayService>()?.Initialize();

                    // 处理启动时携带的命令行命令（如 --identify <path>）
                    await ConsumePendingCliCommandAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "应用启动后初始化失败");
                }
            };

            // Close 拦截：CloseAction=MinimizeToTray 时不真关，只 Hide；
            // TrayService.RequestExit() 已置 IsExitRequested=true 时直接放行
            window.Closing += (_, e) =>
            {
                var tray = Program.Services.GetService<TrayService>();
                var preferences = Program.Services.GetService<DesktopPreferences>();

                var isExit = tray?.IsExitRequested ?? true;
                var closeAction = preferences?.CloseAction ?? CloseAction.Exit;

                // 用户请求退出 OR 没启用 close-to-tray：放行 + 退出整个应用
                if (isExit || closeAction == CloseAction.Exit)
                {
                    try { Program.Services.GetService<WindowManager>()?.CloseAll(); }
                    catch (Exception ex) { Log.Warning(ex, "关闭子窗时异常"); }

                    // OnExplicitShutdown 模式下需要显式 Shutdown
                    desktop.Shutdown();
                    return;
                }

                // close-to-tray：首次提示一次（不阻塞此次最小化，dialog 在隐藏后浮在屏幕上）
                e.Cancel = true;
                if (preferences is not null && !preferences.TrayHintShown)
                {
                    preferences.TrayHintShown = true;
                    preferences.RequestSave();
                    _ = ShowFirstTimeTrayHintAsync();
                }
                window.Hide();
                Log.Information("主窗最小化到托盘（close-to-tray）");
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyPersistedTheme(string? themeName)
    {
        if (Application.Current is null) return;
        var variant = themeName switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
        Application.Current.RequestedThemeVariant = variant;
    }

    /// <summary>
    /// 首次关窗到托盘时的引导提示。fire-and-forget 调用——主窗已经 Hide，
    /// dialog 由 ContentDialog 的 OverlayLayer 浮在桌面上层（不依赖主窗可见）。
    /// </summary>
    private static async Task ShowFirstTimeTrayHintAsync()
    {
        try
        {
            await NineKgConfirmDialog.ShowAsync(null,
                title: "应用仍在托盘运行",
                message: "主窗已最小化到系统托盘。文件夹监控、识别队列继续在后台运行。\n\n" +
                         "· 单击托盘图标 → 重新打开主窗\n" +
                         "· 托盘菜单「退出 NineKgTools」→ 真正终止进程\n" +
                         "· 在「设置 / 外观」可改成「关闭主窗时直接退出」",
                intent: DialogIntent.Info,
                confirmText: "知道了");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "首次托盘提示 dialog 显示失败");
        }
    }

    private static async Task ConsumePendingCliCommandAsync()
    {
        var pending = Program.Pending;
        if (pending is null) return;

        try
        {
            var dispatcher = Program.Services.GetRequiredService<DragDropDispatcher>();
            switch (pending.Cmd?.ToLowerInvariant())
            {
                case "identify" when !string.IsNullOrEmpty(pending.Path):
                    await dispatcher.HandleDropAsync(new[] { pending.Path });
                    break;
                case "quit":
                    Program.Services.GetService<TrayService>()?.RequestExit();
                    break;
                // show-main：本进程已经把主窗显示出来了，no-op
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "消费启动命令失败：{Cmd}", pending.Cmd);
        }
    }
}
