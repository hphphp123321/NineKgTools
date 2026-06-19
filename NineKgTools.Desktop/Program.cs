using System.Runtime.InteropServices;
using Avalonia;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineKgTools.Core.Hosting;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop;

internal static class Program
{
    /// <summary>
    /// 应用全局服务容器。Avalonia App 类、ViewModel 等可通过此静态属性获取已注册服务。
    /// 启动期通过 BootstrapAsync 填充。
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// 单实例 Mutex 名（每用户独立）。
    /// </summary>
    private static string SingleInstanceMutexName => $@"Local\NineKgTools.Desktop.{Environment.UserName}";

    [STAThread]
    public static int Main(string[] args)
    {
        // Velopack 必须在最前：安装/卸载/更新阶段它会拦截 --veloapp-* hook 参数、执行后直接退出进程，
        // 绝不能晚于单实例 Mutex / IPC / Avalonia（否则 hook 期会误触发我们的启动逻辑）。
        // 普通运行下它快速返回，无副作用。
        Velopack.VelopackApp.Build().Run();

        // 解析命令行命令（--identify <path> / --quit / --show-main）
        var pendingCommand = ParseCliCommand(args);
        // --autostart：开机自启拉起，要求静默隐藏到托盘启动（不弹主窗）。不是 IPC 转发命令，是本进程启动模式。
        var startHidden = args.Contains("--autostart", StringComparer.OrdinalIgnoreCase);

        // 单实例守门：如果已有进程在跑 → 把命令转发给它，自己退出
        var mutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, out var isNewInstance);
        if (!isNewInstance)
        {
            mutex.Dispose();
            if (pendingCommand is not null)
            {
                // 转发命令；失败也只能算了——用户至少看到现有进程仍活
                var ok = IpcService.TrySendAsync(pendingCommand).GetAwaiter().GetResult();
                Console.WriteLine(ok ? "已转发命令到现有进程" : "转发命令失败（连接超时）");
            }
            else if (!startHidden)
            {
                // 双击启动 / 无命令——让现有进程把主窗显示出来。
                // 但 --autostart 自启场景下若已有实例（如手动开过）则静默退出，不打扰已运行的窗口。
                _ = IpcService.TrySendAsync(new IpcCommand { Cmd = "show-main" }).GetAwaiter().GetResult();
            }
            return 0;
        }

        StartHidden = startHidden;

        try
        {
            BootstrapAsync().GetAwaiter().GetResult();

            // 启动 BackgroundJobServer 之前清理 hangfire.db 里的孤儿 job——
            // TaskMetadataStore 是 in-memory（IMemoryCache）重启即丢，但 Hangfire SQLite
            // 持久化所有 enqueued/processing/scheduled/retrying job。重启后这些旧 job 被
            // worker 取出时找不到 metadata 会无限报错。Desktop 关窗即"应用重启"，孤儿 job
            // 必然出现，必须开机一并清理。
            CleanupOrphanHangfireJobs();

            // 启动所有 IHostedService（关键：Hangfire.NetCore 的 BackgroundJobServer 是 IHostedService，
            // ASP.NET 下由 WebApplication.Run 自动 Start，Avalonia 没有 IHost runner 必须手动驱动，
            // 否则任务能 enqueue 进 SQLite 但永远不会被 worker 取出执行）
            StartHostedServicesAsync().GetAwaiter().GetResult();

            // IPC server 启动（接收后续 --identify 等转发）
            try { Services.GetService<IpcService>()?.StartServer(); }
            catch (Exception ex) { Log.Warning(ex, "IpcService 启动失败"); }

            // 把首次启动时携带的命令交给本进程处理（在 UI 框架启动后由 OnFrameworkInitializationCompleted 完成主窗后再触发）
            if (pendingCommand is not null)
            {
                Pending = pendingCommand;
            }

            var exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

            // 反向停止 IHostedService（让 Hangfire BackgroundJobServer 优雅退出，
            // 进行中的 job 会被标记为 Aborted，重启后自动 retry 续跑）
            StopHostedServices();

            AppBootstrap.ShutdownCleanup(Services);
            mutex.ReleaseMutex();
            mutex.Dispose();
            return exitCode;
        }
        catch (Exception ex)
        {
            try { Log.Fatal(ex, "桌面端启动失败"); } catch { /* logger 可能未初始化 */ }
            Console.Error.WriteLine($"桌面端启动失败：{ex}");
            return 1;
        }
    }

    /// <summary>
    /// 启动时携带的命令——由 App.OnFrameworkInitializationCompleted 在主窗显示后消费。
    /// </summary>
    public static IpcCommand? Pending { get; private set; }

    /// <summary>
    /// 是否以 <c>--autostart</c> 静默模式启动（开机自启）。为 true 时 App 让主窗启动后立即隐藏到托盘，
    /// 文件夹监控 / 识别队列照常在后台跑；用户从托盘单击可还原主窗。
    /// </summary>
    public static bool StartHidden { get; private set; }

    /// <summary>
    /// 清理 hangfire.db 里所有非终态 job（Enqueued/Processing/Scheduled/Retrying/Awaiting/Failed）。
    ///
    /// 桌面端 TaskMetadataStore 用 IMemoryCache 存 ITask 实例，重启即丢；但 Hangfire SQLite
    /// 把 jobId 持久化在 hangfire.db。重启后 BackgroundJobServer 会把旧 job 取出送进
    /// `UnifiedTaskService.ExecuteTaskAsync(taskId)`，metadata 已不在 → 抛
    /// `InvalidOperationException("未找到任务元数据")` → 触发 SQLite invisibility timeout 重新拾取
    /// → 同 job 多次失败日志爆炸。Desktop 关窗就是"应用重启"，所以必须每次启动清。
    ///
    /// 保留 Succeeded / Deleted 状态作历史；只清还没跑完的非终态 job——它们已经无法恢复。
    /// </summary>
    private static void CleanupOrphanHangfireJobs()
    {
        try
        {
            var storage = Services.GetService<JobStorage>();
            if (storage is null)
            {
                Log.Debug("CleanupOrphanHangfireJobs: 找不到 JobStorage，跳过");
                return;
            }
            // 强制赋给 Current（BackgroundJob.Delete 走 JobStorage.Current）
            JobStorage.Current = storage;

            var monitor = storage.GetMonitoringApi();
            var deleted = 0;
            const int batch = 1000; // 单批最多 1000，避免一次拉太多

            // 1. Enqueued 队列里的 job
            foreach (var queue in monitor.Queues())
            {
                int offset = 0;
                while (true)
                {
                    var page = monitor.EnqueuedJobs(queue.Name, offset, batch);
                    if (page.Count == 0) break;
                    foreach (var entry in page)
                    {
                        if (BackgroundJob.Delete(entry.Key)) deleted++;
                    }
                    if (page.Count < batch) break;
                    offset += batch;
                }
            }

            // 2. Processing/Scheduled/Retrying/Failed/Awaiting —— 全是非终态遗留
            DeletePagedState(monitor.ProcessingJobs, ref deleted, batch);
            DeletePagedState(monitor.ScheduledJobs, ref deleted, batch);
            DeletePagedState(monitor.FailedJobs, ref deleted, batch);
            // Hangfire 没有公开的 RetryingJobs / AwaitingJobs API（不同版本差异），
            // 上面 Failed 抓一遍已经覆盖大部分卡死 job

            if (deleted > 0)
                Log.Information("启动清理: 删除 {Count} 个 Hangfire 孤儿 job（重启后 metadata 已丢失）", deleted);
            else
                Log.Debug("启动清理: 无 Hangfire 孤儿 job");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CleanupOrphanHangfireJobs 失败——可能 hangfire.db 还未初始化");
        }
    }

    private static void DeletePagedState<TJob>(
        Func<int, int, Hangfire.Storage.Monitoring.JobList<TJob>> fetcher,
        ref int deletedCounter, int batch)
    {
        try
        {
            int offset = 0;
            while (true)
            {
                var page = fetcher(offset, batch);
                if (page.Count == 0) break;
                foreach (var entry in page)
                {
                    if (BackgroundJob.Delete(entry.Key)) deletedCounter++;
                }
                if (page.Count < batch) break;
                offset += batch;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DeletePagedState 子清理失败");
        }
    }

    /// <summary>
    /// 启动所有通过 DI 注册的 IHostedService。Hangfire.NetCore 的
    /// BackgroundJobServerHostedService 在此被驱动起来——没有这步，任务能
    /// enqueue 进 SQLite 但永远不会被 worker 取出执行。
    /// </summary>
    private static async Task StartHostedServicesAsync()
    {
        var hosted = Services.GetServices<IHostedService>().ToList();
        var started = 0;
        foreach (var svc in hosted)
        {
            try
            {
                await svc.StartAsync(CancellationToken.None);
                started++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "IHostedService 启动失败：{Type}", svc.GetType().Name);
            }
        }
        Log.Information("已启动 {Started}/{Total} 个 IHostedService（含 Hangfire BackgroundJobServer）",
            started, hosted.Count);
    }

    /// <summary>
    /// 反向停止所有 IHostedService。让 Hangfire 进行中的 job 优雅 abort（重启后会被
    /// SQLite 存储里的 ProcessingState 检测到并自动 retry）。
    /// </summary>
    private static void StopHostedServices()
    {
        try
        {
            var hosted = Services.GetServices<IHostedService>().Reverse().ToList();
            foreach (var svc in hosted)
            {
                try { svc.StopAsync(CancellationToken.None).GetAwaiter().GetResult(); }
                catch (Exception ex) { Log.Warning(ex, "IHostedService 停止失败：{Type}", svc.GetType().Name); }
            }
        }
        catch (Exception ex) { Log.Warning(ex, "StopHostedServices 异常"); }
    }

    /// <summary>
    /// 把命令行参数解析为 IpcCommand。识别：
    /// <c>--identify &lt;path&gt;</c> / <c>--quit</c> / <c>--show-main</c>
    /// 不识别返回 null。
    /// </summary>
    private static IpcCommand? ParseCliCommand(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--identify" when i + 1 < args.Length:
                    return new IpcCommand { Cmd = "identify", Path = args[i + 1] };
                case "--rescan-folder" when i + 1 < args.Length:
                    return new IpcCommand { Cmd = "rescan-folder", Path = args[i + 1] };
                case "--quit":
                    return new IpcCommand { Cmd = "quit" };
                case "--show-main":
                    return new IpcCommand { Cmd = "show-main" };
            }
        }
        return null;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static async Task BootstrapAsync()
    {
        var dataDir = GetPlatformDataDirectory();
        EnsureConfigBootstrap(dataDir);

        var config = await AppBootstrap.InitializeConfigAsync(
            new AppBootstrapOptions { DataDirectory = dataDir });
        var logger = AppBootstrap.ConfigureLogger(config);
        Log.Information("桌面端数据目录：{DataDir}", dataDir);

        // 加载桌面端独有的 UI 偏好（关窗行为 / 主题 / 窗口位置等），与 config.yaml 解耦
        var preferences = DesktopPreferences.Load(dataDir);

        var services = new ServiceCollection();
        AppBootstrap.ConfigureCoreServices(services, config, logger);
        ConfigureHangfire(services, config);
        ConfigureDesktopServices(services, preferences);

        Services = services.BuildServiceProvider();

        await AppBootstrap.RunStartupAsync(Services);
    }

    /// <summary>
    /// 桌面端独有服务：NavigationService（Singleton 全局导航状态）+ 全部 ViewModel（Transient，每次导航新实例）。
    /// </summary>
    private static void ConfigureDesktopServices(IServiceCollection services, DesktopPreferences preferences)
    {
        services.AddSingleton(preferences);
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ImageCacheService>();
        services.AddSingleton<WindowManager>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<DragDropDispatcher>();
        services.AddSingleton<WindowStateService>();
        services.AddSingleton<IpcService>();
        services.AddSingleton<ShellIntegrationService>();
        services.AddSingleton<AutoStartService>();
        // 自动更新（Velopack）——仅 Velopack 安装版生效，dev/portable 下 IsSupported=false 全 no-op
        services.AddSingleton<UpdateService>();
        // 共享交互式识别流程（选项 / 进度+诊断 / 预览三步链；MediaDetailVM、PendingMediaVM 都会用）
        services.AddSingleton<IdentificationFlowService>();

        // Window-level VM
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<GlobalSearchFlyoutViewModel>();

        // Page VMs（PageViewModelBase 子类，由 NavigationService 解析）
        services.AddTransient<HomeViewModel>();
        services.AddTransient<MediaOverviewViewModel>();
        services.AddTransient<PendingMediaViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<WatchFoldersViewModel>();
        services.AddTransient<BackgroundTasksViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<TagsMappingsViewModel>();
        services.AddTransient<CreatorsViewModel>();
        services.AddTransient<CirclesViewModel>();
        services.AddTransient<SearchResultViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<WebsitesViewModel>();
        services.AddTransient<SettingsViewModel>();
        // 媒体详情 VM（同时被 NavigationService 内嵌页 / WindowManager 独立窗 复用）
        // 升级为 PageViewModelBase 之后由 NavigationService 解析，独立窗 OpenMediaDetail 也走同一 DI
        services.AddTransient<MediaDetailViewModel>();
    }

    /// <summary>
    /// Hangfire 注册：单 BackgroundJobServer + **MemoryStorage**（不持久化）。
    ///
    /// 为什么不用 SQLite 持久化？Hangfire.SQLite 1.4.2 在桌面端高并发 worker 下有严重 fetch
    /// 竞态——同一 jobId 被多 worker 拾取多次执行（实测 60 倍重复，父任务每次重复都
    /// CreateChildTasksAsync + 全量 enqueue 子任务，雪崩出 60×7=420 个孤儿子任务），
    /// 且 worker 完成后 IFetchedJob.RemoveFromQueue 失效，jobId 被反复 fetch 永不进入终态。
    ///
    /// 桌面端用户心智下"关进程=任务停止"——不需要跨重启续跑。MemoryStorage 与 Web 端一致，
    /// 关进程即丢任务，重启从干净状态开始，零孤儿、零竞态。TaskMetadataStore 本就用
    /// IMemoryCache（in-memory），生命周期天然对齐。
    ///
    /// 单 server + 所有队列共享 worker pool；MaxConcurrentIdentificationTasks 限制改用
    /// 队列优先级实现：identification 排在 default 之前自然排队。严格 N 并发限制不保证
    /// （用户场景下识别受网络限速主导，无伤大雅）。
    /// </summary>
    private static void ConfigureHangfire(IServiceCollection services, NineKgTools.Core.Services.Configs.Config config)
    {
        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSerilogLogProvider()
            .UseMemoryStorage());

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 0 });

        var identificationWorkers = config.Tasks?.MaxConcurrentIdentificationTasks ?? 5;
        var defaultWorkers = Environment.ProcessorCount * 2;
        var totalWorkers = Math.Max(defaultWorkers, identificationWorkers);

        // 单 server，队列优先级从高到低排列：critical / high 高频系统任务优先；
        // identification 排在 default 之前（让识别在普通任务前进队列）；low / background 兜底
        services.AddHangfireServer(o =>
        {
            o.Queues = new[] { "critical", "high", "identification", "default", "low", "background" };
            o.WorkerCount = totalWorkers;
            o.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            o.ServerName = $"{Environment.MachineName}:NineKgTools.Desktop";
        });
    }

    /// <summary>
    /// 平台特定数据目录：Win → LocalAppData，Mac → ~/Library/Application Support，Linux → $XDG_DATA_HOME。
    /// 该目录下会包含 Config/、Database/、Logs/、.cache/ 等子目录，与 Web 端独立隔离。
    /// </summary>
    private static string GetPlatformDataDirectory()
    {
        // Portable 模式：exe 同目录存在 .portable 标记文件 → 数据落 <exeDir>/data，
        // 不写 LocalAppData。让用户把整个目录拷到 U 盘 / 另一台机器带着数据走。
        // 标记文件需用户手动创建（不是默认值），避免安装版被误判 portable 把数据写进 Program Files。
        // 单文件发布下 AppContext.BaseDirectory 指向 exe 所在目录（非临时解压目录），正合适。
        var exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, ".portable")))
        {
            return Path.Combine(exeDir, "data");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NineKgTools.Desktop");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "NineKgTools.Desktop");
        }
        // Linux / 其他
        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(xdg))
        {
            xdg = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");
        }
        return Path.Combine(xdg, "NineKgTools.Desktop");
    }

    /// <summary>
    /// 首次启动时把 Config/ 下所有 yaml 模板（config.example.yaml / tags.yaml 等）复制到 dataDir/Config/。
    /// 模板源优先来自可执行文件同目录的 Config/（发布场景，由 csproj <None Include="..\Config\*.yaml"> 提供），
    /// dev 场景回退到 sln 根 Config/。
    /// 已存在的目标文件不覆盖（保留用户修改）；config.yaml 不存在时由 config.example.yaml 复制一份作为初始值。
    /// </summary>
    private static void EnsureConfigBootstrap(string dataDir)
    {
        var sourceConfigDir = FindSourceConfigDir();
        if (sourceConfigDir == null)
        {
            throw new DirectoryNotFoundException(
                $"首次启动需要 Config/ 模板目录（含 config.example.yaml + tags.yaml 等），但未在 '{AppContext.BaseDirectory}' 或上游 sln 目录找到。");
        }

        var targetConfigDir = Path.Combine(dataDir, "Config");
        Directory.CreateDirectory(targetConfigDir);

        // 复制所有 yaml 模板（example + tags 等运行期数据），不覆盖已存在文件
        foreach (var yamlSrc in Directory.GetFiles(sourceConfigDir, "*.yaml"))
        {
            var fileName = Path.GetFileName(yamlSrc);
            // 跳过用户态的 config.yaml（避免覆盖用户改过的本地配置；首次启动会从 example 复制）
            if (string.Equals(fileName, "config.yaml", StringComparison.OrdinalIgnoreCase)) continue;

            var dst = Path.Combine(targetConfigDir, fileName);
            if (!File.Exists(dst)) File.Copy(yamlSrc, dst, overwrite: false);
        }

        // 首次启动 config.yaml 不存在 → 从 example 复制一份作为初始
        var targetConfig = Path.Combine(targetConfigDir, "config.yaml");
        if (!File.Exists(targetConfig))
        {
            var example = Path.Combine(targetConfigDir, "config.example.yaml");
            if (File.Exists(example))
            {
                File.Copy(example, targetConfig, overwrite: false);
            }
        }
    }

    private static string? FindSourceConfigDir()
    {
        // 1. 与可执行文件同级的 Config/（发布场景，由 csproj 的 None Include 复制过来）
        var alongsideExe = Path.Combine(AppContext.BaseDirectory, "Config");
        if (Directory.Exists(alongsideExe) && Directory.GetFiles(alongsideExe, "*.yaml").Any())
            return alongsideExe;

        // 2. 开发场景：从 BaseDirectory 向上查找 .sln，定位 sln_dir/Config/
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir != null; i++)
        {
            if (dir.GetFiles("*.sln").Any())
            {
                var devPath = Path.Combine(dir.FullName, "Config");
                if (Directory.Exists(devPath)) return devPath;
                break;
            }
            dir = dir.Parent;
        }
        return null;
    }
}
