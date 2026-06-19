using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Hosting;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.Views;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop;

public partial class App : Application
{
    /// <summary>
    /// 主窗 Closing 处理器调 desktop.Shutdown 时，DoShutdown 又会再次调
    /// window.OnClosing → 同一个 handler 二次重入。无 guard 会无限递归直到 stack overflow。
    /// 这里仅在第一次进入时执行真正的关闭逻辑，后续重入直接放行。
    /// </summary>
    private bool _shuttingDown;

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

            // 禁用 FluentAvalonia 全局动画——根因：FANavigationView 内部
            // PlayIndicatorAnimations 用 StepEasingFunction { Steps = 5 } 量化 position
            // 关键帧的进度（NavigationView.cs:3447），把"从旧位置滑到新位置"切成 5 档台阶。
            // 相邻 tab 距离小（每档几像素）肉眼看不出量化；跨多 tab 时每档几十上百像素，
            // 视觉上变成"几次跳跃" + 后续 400ms 静止 hold——感觉像低帧率瞬移。
            // FA 没有公开 API 单独关 indicator 动画或调 Steps；只能整体关。
            // trade-off：FAContentDialog / TeachingTip 等的 fade-in/scale-in 也会去掉，
            // 改成直接显示（这在桌面端很常见，VS/Rider 也是 instant 弹出）。
            FluentAvalonia.Core.FAUISettings.SetAnimationsEnabledAtAppLevel(false);

            // 应用启动时按 DesktopPreferences 还原主题
            var prefs = Program.Services.GetRequiredService<DesktopPreferences>();
            ApplyPersistedTheme(prefs.Theme);

            var vm = Program.Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            // --autostart 静默启动：lifetime 仍会自动 Show 主窗，无法跳过。这里先让它以
            // Minimized + 不进任务栏的方式打开（屏幕上不可见、无任务栏闪烁），待 Opened 里
            // 跑完启动任务 + 托盘初始化后再 Hide() 彻底隐藏。托盘单击经 TrayService.ShowMainWindow
            // 还原（会重置 WindowState=Normal + ShowInTaskbar=true）。
            if (Program.StartHidden)
            {
                window.WindowState = Avalonia.Controls.WindowState.Minimized;
                window.ShowInTaskbar = false;
            }

            // 主窗显示后再触发 AfterStartup（拉起文件夹监控、定时任务），避免阻塞 UI 初始化。
            //
            // **once-only guard**：close-to-tray 后用户从托盘单击恢复主窗会再调 main.Show()，
            // Avalonia 12 的 Window.Opened 在每次 Show() 都会触发——若不挡，会让
            // RunAfterStartupAsync 重跑，里面 StartProcessConfiguredFolders 对每个监视
            // 文件夹除了启动监控外还会调 IdentifyBatchMedia 把整个文件夹重新批量识别一遍。
            // 用闭包 boolean 兜住，启动期任务只跑一次。
            bool startupTasksRan = false;
            window.Opened += async (_, _) =>
            {
                if (startupTasksRan) return;
                startupTasksRan = true;

                try
                {
                    await vm.InitializeAsync();
                    await AppBootstrap.RunAfterStartupAsync(Program.Services);

                    // Tray 必须在 MainWindow.Opened 之后初始化——某些平台（macOS）
                    // TrayIcon 注册需要 Application 已经显示过窗口
                    Program.Services.GetService<TrayService>()?.Initialize();

                    // --autostart：托盘已就绪，把启动期那个 Minimized 隐形窗彻底隐藏到托盘
                    if (Program.StartHidden)
                    {
                        window.Hide();
                        Log.Information("--autostart：启动完成，主窗已隐藏到托盘");
                    }

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
                // 重入保护：desktop.Shutdown() 内部会逐个关闭窗口又触发 Closing，
                // 不挡就会无限递归直到 stack overflow（实测）
                if (_shuttingDown) return;

                var tray = Program.Services.GetService<TrayService>();
                var preferences = Program.Services.GetService<DesktopPreferences>();

                var isExit = tray?.IsExitRequested ?? true;
                var closeAction = preferences?.CloseAction ?? CloseAction.Exit;

                // 用户请求退出 OR 没启用 close-to-tray：放行 + 退出整个应用
                if (isExit || closeAction == CloseAction.Exit)
                {
                    _shuttingDown = true;
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
                         "· 在「设置 / 通用」可改成「关闭主窗时直接退出」",
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
                case "rescan-folder" when !string.IsNullOrEmpty(pending.Path):
                    var taskId = await dispatcher.RescanFolderAsync(pending.Path);
                    if (!string.IsNullOrEmpty(taskId))
                    {
                        // Hangfire MemoryStorage 在进程退出时丢任务——必须等父任务跑完再退。
                        // 子任务（每个媒体的爬取 + 入库 + 写 .cache）也在父任务的"等子任务完成"环节里同步。
                        await WaitForTaskCompletionAsync(taskId, TimeSpan.FromMinutes(15));
                    }
                    // 完成后退出（一次性命令，不留进程）
                    Program.Services.GetService<TrayService>()?.RequestExit();
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

    /// <summary>
    /// 轮询直到父任务真正执行完毕——给 CLI 一次性命令（--rescan-folder 等）等待 Hangfire 后台任务完成用，
    /// 否则进程提前退出会让 MemoryStorage 里的 Job 全部丢失。
    ///
    /// 终止信号优先使用 <see cref="TaskMetadataStore.GetTaskAsync"/> 返回 null
    /// （UnifiedTaskService.ExecuteParentTaskAsync 的 finally 块在父任务彻底结束后才清理元数据）。
    /// 这比 progress.Status 更可靠：observation 显示父任务还在运行子任务时
    /// progress.Status 已经被某条不明路径标记为 Failed（疑似 reporter 串扰），导致旧实现误判提前退出。
    /// </summary>
    private static async Task WaitForTaskCompletionAsync(string taskId, TimeSpan timeout)
    {
        var metadataStore = Program.Services.GetService<TaskMetadataStore>();
        var progressService = Program.Services.GetService<TaskProgressService>();
        if (metadataStore is null)
        {
            Log.Warning("WaitForTaskCompletionAsync: TaskMetadataStore 未注册，无法轮询");
            return;
        }

        var deadline = DateTime.UtcNow + timeout;
        Log.Information("WaitForTaskCompletionAsync: 等待任务完成 TaskId={TaskId}, Timeout={Timeout}s",
            taskId, timeout.TotalSeconds);

        // 进入轮询前 metadata 一定已经存在（IdentifyBatchMedia 同步阶段就把 task 存了）。
        // 这里轮询直到 metadata 被父任务的 finally 块清理掉——这是父任务真正结束的唯一可靠信号。
        while (DateTime.UtcNow < deadline)
        {
            var task = await metadataStore.GetTaskAsync(taskId);
            if (task is null)
            {
                var status = progressService?.GetProgress(taskId)?.Status;
                Log.Information("WaitForTaskCompletionAsync: TaskId={TaskId} 元数据已清理，任务结束 (Status={Status})",
                    taskId, status?.ToString() ?? "Unknown");
                return;
            }
            await Task.Delay(500);
        }

        Log.Warning("WaitForTaskCompletionAsync: TaskId={TaskId} 超时（{Timeout}s 仍未结束）",
            taskId, timeout.TotalSeconds);
    }
}
