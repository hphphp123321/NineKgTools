using System.Runtime.InteropServices;
using Avalonia;
using Hangfire;
using Hangfire.SQLite;
using Microsoft.Extensions.DependencyInjection;
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
        // 解析命令行命令（--identify <path> / --quit / --show-main）
        var pendingCommand = ParseCliCommand(args);

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
            else
            {
                // 双击启动 / 无命令——让现有进程把主窗显示出来
                _ = IpcService.TrySendAsync(new IpcCommand { Cmd = "show-main" }).GetAwaiter().GetResult();
            }
            return 0;
        }

        try
        {
            BootstrapAsync().GetAwaiter().GetResult();

            // IPC server 启动（接收后续 --identify 等转发）
            try { Services.GetService<IpcService>()?.StartServer(); }
            catch (Exception ex) { Log.Warning(ex, "IpcService 启动失败"); }

            // 把首次启动时携带的命令交给本进程处理（在 UI 框架启动后由 OnFrameworkInitializationCompleted 完成主窗后再触发）
            if (pendingCommand is not null)
            {
                Pending = pendingCommand;
            }

            var exitCode = BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

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
    /// 把命令行参数解析为 IpcCommand。识别：
    /// `--identify &lt;path&gt;` / `--quit` / `--show-main`
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

        // Window-level VM
        services.AddTransient<MainWindowViewModel>();

        // Page VMs（PageViewModelBase 子类，由 NavigationService 解析）
        services.AddTransient<HomeViewModel>();
        services.AddTransient<MediaOverviewViewModel>();
        services.AddTransient<PendingMediaViewModel>();
        services.AddTransient<SourcesViewModel>();
        services.AddTransient<BackgroundTasksViewModel>();
        services.AddTransient<TagsViewModel>();
        services.AddTransient<CreatorsViewModel>();
        services.AddTransient<FavoritesViewModel>();
        services.AddTransient<WebsitesViewModel>();
        services.AddTransient<SettingsViewModel>();

        // 详情独立窗口 VM（每次 OpenMediaDetail 调用时新建实例）
        services.AddTransient<MediaDetailViewModel>();
    }

    /// <summary>
    /// Hangfire 注册：双 BackgroundJobServer 队列分离 + SQLite 持久化存储。
    /// 持久化让识别 / 监控等任务跨重启续跑——用户关窗后重开能看到原本进行中的任务恢复。
    /// 存储文件位于 config.Database.HangfirePath（默认 dataDir/Database/hangfire.db）。
    /// </summary>
    private static void ConfigureHangfire(IServiceCollection services, NineKgTools.Core.Services.Configs.Config config)
    {
        // Hangfire.SQLite 1.4.2 用字符串里是否含 ';' 区分"connection string 字面量"vs"app.config 节点名"。
        // Core 的 GetHangfireConnectionString 不加分号；这里追加一个，避免被误判成节点名。
        var hangfireConn = config.Database.GetHangfireConnectionString();
        if (!hangfireConn.Contains(';')) hangfireConn += ";";

        var hangfireDir = Path.GetDirectoryName(config.Database.HangfirePath);
        if (!string.IsNullOrEmpty(hangfireDir)) Directory.CreateDirectory(hangfireDir);

        services.AddHangfire(c => c
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSerilogLogProvider()
            .UseSQLiteStorage(hangfireConn));

        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 0 });

        var identificationWorkers = config.Tasks?.MaxConcurrentIdentificationTasks ?? 5;

        services.AddHangfireServer(o =>
        {
            o.Queues = new[] { "critical", "high", "default", "low", "background" };
            o.WorkerCount = Environment.ProcessorCount * 2;
            o.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            o.ServerName = $"{Environment.MachineName}:NineKgTools.Desktop";
        });
        services.AddHangfireServer(o =>
        {
            o.Queues = new[] { "identification" };
            o.WorkerCount = identificationWorkers;
            o.SchedulePollingInterval = TimeSpan.FromSeconds(15);
            o.ServerName = $"{Environment.MachineName}:NineKgTools.Desktop-Identification";
        });
    }

    /// <summary>
    /// 平台特定数据目录：Win → LocalAppData，Mac → ~/Library/Application Support，Linux → $XDG_DATA_HOME。
    /// 该目录下会包含 Config/、Database/、Logs/、.cache/ 等子目录，与 Web 端独立隔离。
    /// </summary>
    private static string GetPlatformDataDirectory()
    {
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
