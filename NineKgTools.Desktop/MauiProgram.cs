using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Services;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Logger;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace NineKgTools.Desktop;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// 初始化配置
		var config = new Config();
		config.InitConfig().GetAwaiter().GetResult();
		
		var logger = new LoggerService(config);
		logger.ConfigureLogger();
		Log.Information("Desktop应用初始化日志完成");
		
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
		
		// 注册配置和日志
		builder.Services.AddSingleton<Config>(_ => config);
		builder.Services.AddSingleton<LoggerService>(_ => logger);
		
		// 配置数据库
		var connectionString = "Data Source=Database/database.db";
		var dbDir = Path.GetDirectoryName(connectionString.Split('=')[1]);
		Directory.CreateDirectory(dbDir);
		
		builder.Services.AddDbContext<MediaDbContext>(options =>
			options.UseSqlite(connectionString,
				o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
			.EnableSensitiveDataLogging(),
			contextLifetime: ServiceLifetime.Scoped);
		
		// 添加项目相关服务
		builder.Services.AddNineKgToolsService();
		
		// 添加MudBlazor服务
		builder.Services.AddMudServices();
		

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
