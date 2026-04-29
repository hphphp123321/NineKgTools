using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Cache;
using NineKgTools.Core.Services.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.Audio;
using NineKgTools.Core.Services.Media.Game;
using NineKgTools.Core.Services.Media.Picture;
using NineKgTools.Core.Services.Media.Text;
using NineKgTools.Core.Services.Media.Video;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Search;
using NineKgTools.Core.Services.Source;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Core.Services.Tasks.ScheduledTasks;
using NineKgTools.Core.Services.Vectors;
using NineKgTools.Core.Services.Websites;
using NineKgTools.Core.Services.Websites.Bangumi;
using NineKgTools.Core.Services.Websites.DLsite;
using NineKgTools.Core.Services.Websites.Steam;
using NineKgTools.Core.Services.Files.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Net;
using Serilog;

namespace NineKgTools.Core.Services;

public static class ServiceCollectionExtensions
{
    public static
#nullable disable
        IServiceCollection AddNineKgToolsService(this IServiceCollection services)
    {
        // 添加统一任务服务
        services.AddSingleton<UnifiedTaskService>();
        services.AddSingleton<TaskMetadataStore>();
        services.AddSingleton<ScheduledTaskFactory>();
        services.AddSingleton<MonitorService>();  // 添加文件夹监控服务
        services.AddScoped<HttpService>();
        
        // 添加缓存服务
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 10000; // 设置缓存容量限制
        });
        
        // 添加向量数据库服务（Singleton：SQLite 不支持并发写入，需共享实例 + 写锁串行化）
        services.AddSingleton(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            if (config.Ai?.UseAi == true && config.Ai?.Vector?.Enable == true && config.Ai?.Vector?.Db != null)
            {
                try
                {
                    var vectorDb = new VectorService(config.Ai.Vector.Db);
                    return vectorDb;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创建向量数据库服务失败");
                    return null;
                }
            }
            return null;
        });

        services.AddScoped(provider =>
        {
            var config = provider.GetRequiredService<Config>();
            if (config.Ai?.UseAi == true && config.Ai?.Vector?.Enable == true && config.Ai?.Vector?.Db != null)
            {
                var openaiService = provider.GetRequiredService<OpenaiService>();
                var cache = provider.GetRequiredService<IMemoryCache>();
                return new VectorEmbeddingService(openaiService, cache, config.Ai.Vector.Db);
            }
            return null;
        });
        
        // 添加底层服务
        services.AddScoped<TagMappingService>();
        services.AddScoped<TagMatchingService>();
        services.AddScoped<TagService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<FavoriteService>();
        services.AddScoped<ImageService>();
        
        // 添加媒体相关服务
        services.AddScoped<SourceService>();
        services.AddScoped<CreatorService>();
        services.AddScoped<PictureMediaService>();
        services.AddScoped<VideoMediaService>();
        services.AddScoped<GameMediaService>();
        services.AddScoped<AudioMediaService>();
        services.AddScoped<TextMediaService>();
        services.AddScoped<MediaService>();
        services.AddScoped<PendingIdentificationService>();
        services.AddScoped<MediaNameSplitterService>();
        
        // 添加网站服务
        services.AddScoped<IWebsite, DLsiteService>();
        services.AddScoped<IWebsite, BangumiService>();
        services.AddScoped<IWebsite, SteamService>();

        services.AddScoped<WebsiteService>();
        
        // 添加文件服务
        services.AddScoped<FilesService>();

        // 添加文件浏览器服务（根据访问上下文动态创建）
        services.AddScoped<IFileExplorerService>(sp =>
        {
            var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
            var isLocal = IsLocalAccess(httpContextAccessor);
            return FileExplorerServiceFactory.Create(isLocal);
        });
        
        // 添加其他服务
        services.AddScoped<OpenaiService>();
        
        // 添加搜索服务
        services.AddScoped<GlobalSearchService>();
        services.AddSingleton<CancellableSearchManager>();
        services.AddScoped<SearchCacheManager>();
        
        // 添加识别缓存服务
        services.AddSingleton<IdentificationCacheService>();
        
        // 统一任务进度服务
        services.AddSingleton<TaskProgressService>();
        
        // 添加定时任务实现
        services.AddTransient<CacheCleanupTask>();
        services.AddTransient<MediaCleanupTask>();
        services.AddTransient<TagVectorSyncTask>();
        services.AddTransient<MediaVectorSyncTask>();
        services.AddTransient<PendingIdentificationCleanupTask>();
        return services;
    }
    
    public static async Task<IServiceProvider> InitNineKgToolsService(this IServiceProvider services)
    {
        await services.GetRequiredService<TagService>().InitializeTagsDbFromYaml();
        await services.GetRequiredService<CategoryService>().InitializeCategoriesDb();
        await services.GetRequiredService<FavoriteService>().InitializeFavoritesDb();
        await services.GetRequiredService<SourceService>().InitializeMediaSourcesDb();
        
        // 初始化向量数据库（如果启用）
        var config = services.GetRequiredService<Config>();
        if (config.Ai?.UseAi == true && config.Ai?.Vector?.Enable == true)
        {
            var vectorDb = services.GetService<VectorService>();
            if (vectorDb != null)
            {
                try
                {
                    await vectorDb.InitializeAsync();
                    // 日志已在 VectorService.InitializeAsync 中输出，避免重复
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "向量数据库初始化失败");
                }
            }
        }
        
        services.GetRequiredService<WebsiteService>().LogWebsites();
        
        // 预热搜索索引（如果启用）
        if (config.Search?.EnableGlobalSearch == true)
        {
            var searchService = services.GetService<GlobalSearchService>();
            if (searchService != null)
            {
                try
                {
                    await searchService.WarmupSearchIndexAsync();
                    Log.Information("搜索索引预热成功");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "搜索索引预热失败");
                }
            }
        }

        return services;
    }

    public static async Task<IServiceProvider> AfterAppStartup(this IServiceProvider services)
    {
        await services.GetRequiredService<FilesService>().StartProcessConfiguredFolders();
        
        // 延迟初始化定时任务，避免在应用启动时立即执行
        // 这可以防止在 DbContext 还未完全初始化时执行任务导致的循环依赖问题
        
        await Task.Delay(TimeSpan.FromSeconds(10)); // 等待10秒确保应用完全启动
        try
        {
            // 获取 UnifiedTaskService 实例，触发构造函数中的定时任务初始化
            _ = services.GetRequiredService<UnifiedTaskService>();
            Log.Information("定时任务已初始化");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化定时任务失败");
        }
        
        return services;
    }

    /// <summary>
    /// 判断当前请求是否为本地访问
    /// </summary>
    private static bool IsLocalAccess(IHttpContextAccessor? httpContextAccessor)
    {
        var context = httpContextAccessor?.HttpContext;
        if (context == null)
        {
            // 如果无法获取 HttpContext，默认为本地访问
            return true;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
            return true;

        // 判断是否为本地 IP（回环地址或与本地地址相同）
        return IPAddress.IsLoopback(remoteIp)
               || remoteIp.Equals(context.Connection.LocalIpAddress);
    }
}