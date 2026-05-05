using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Services;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Logger;
using Serilog;

namespace NineKgTools.Core.Hosting;

/// <summary>
/// 桌面端 / Web 端共享的应用启动选项。
/// </summary>
public sealed class AppBootstrapOptions
{
    /// <summary>
    /// 应用数据根目录。设置后会把进程工作目录切到该目录，让 Config / Logs / Database 等相对路径
    /// 都归属此目录，达到"桌面端独立 db 文件"的效果。
    /// Web 端通常不设置（沿用启动工作目录）；Desktop 端传入平台特定的 LocalAppData 子目录。
    /// </summary>
    public string? DataDirectory { get; init; }
}

/// <summary>
/// Web 与 Desktop 共享的核心 Host 启动逻辑。覆盖：
/// 1. Config 加载（含 DataDirectory 覆盖）
/// 2. Serilog 初始化
/// 3. MediaDbContext + DbContextFactory 注册
/// 4. <see cref="ServiceCollectionExtensions.AddNineKgToolsService"/> 业务服务注册
/// 5. 数据库 schema 迁移（baseline / migrate / EnsureCreated 三分支由 MediaDbContextMigrator 处理）
/// 6. <see cref="ServiceCollectionExtensions.InitNineKgToolsService"/> Core 初始化
/// 7. <see cref="ServiceCollectionExtensions.AfterAppStartup"/> 文件夹监控 / 定时任务启动
/// 8. 关闭清理（停止文件夹监控 + 刷日志）
///
/// 不覆盖（由调用方按宿主框架自行处理）：Hangfire 注册、认证、UI 框架特定服务。
/// </summary>
public static class AppBootstrap
{
    public static async Task<Config> InitializeConfigAsync(AppBootstrapOptions? options = null)
    {
        if (!string.IsNullOrEmpty(options?.DataDirectory))
        {
            Directory.CreateDirectory(options.DataDirectory);
            Environment.CurrentDirectory = options.DataDirectory;
        }

        var config = new Config();
        await config.InitConfig();
        return config;
    }

    public static LoggerService ConfigureLogger(Config config)
    {
        var logger = new LoggerService(config);
        logger.ConfigureLogger();
        Log.Information("初始化日志完成");
        return logger;
    }

    /// <summary>
    /// 注册 Config / Logger / MediaDbContext / 业务服务到 DI 容器。
    /// 调用前请先 <see cref="InitializeConfigAsync"/> + <see cref="ConfigureLogger"/>。
    /// </summary>
    public static void ConfigureCoreServices(IServiceCollection services, Config config, LoggerService logger)
    {
        services.AddSingleton(config);
        services.AddSingleton(logger);

        var connectionString = config.Database.GetConnectionString();
        var dbPath = config.Database.Path;
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        services.AddDbContext<MediaDbContext>(options =>
                options.UseSqlite(connectionString,
                        o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .EnableSensitiveDataLogging()
            , contextLifetime: ServiceLifetime.Scoped);

        services.AddDbContextFactory<MediaDbContext>(options =>
                options.UseSqlite(connectionString,
                        o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                    .EnableSensitiveDataLogging()
            , lifetime: ServiceLifetime.Scoped);

        services.AddNineKgToolsService();
    }

    /// <summary>
    /// 启动期：DB schema 迁移 + Core 业务初始化（标签/分类/收藏夹/媒体源/向量库等）。
    /// 调用方应在 ConfigureCoreServices 之后、应用启动监听前调用。
    /// </summary>
    public static async Task RunStartupAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var dbContext = sp.GetRequiredService<MediaDbContext>();
        await MediaDbContextMigrator.EnsureSchemaAsync(dbContext);

        await sp.InitNineKgToolsService();
    }

    /// <summary>
    /// 应用启动后调用：拉起文件夹监控、定时任务等需要 ApplicationStarted 后才能跑的工作。
    /// </summary>
    public static async Task RunAfterStartupAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.AfterAppStartup();
    }

    /// <summary>
    /// 应用停止时清理资源（停止文件夹监控 + 刷日志）。
    /// </summary>
    public static void ShutdownCleanup(IServiceProvider services)
    {
        Log.Information("应用正在停止...");

        try
        {
            using var scope = services.CreateScope();
            var monitorService = scope.ServiceProvider.GetRequiredService<MonitorService>();
            monitorService.StopAllMonitoring();
            Log.Information("已停止所有文件夹监控");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "停止文件夹监控时发生错误");
        }

        Log.CloseAndFlush();
    }
}
