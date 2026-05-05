using System.Net;
using NineKgTools.Core.Hosting;
using NineKgTools.Core.Services.Auth;
using MudBlazor.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using NineKgTools;
using NineKgTools.Auth;
using Serilog;

// Config + Logger 由 AppBootstrap 统一初始化（与 Desktop 共享）
var config = await AppBootstrap.InitializeConfigAsync();
var logger = AppBootstrap.ConfigureLogger(config);

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// 配置端口等信息
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Parse(config.App.WebHost), config.App.WebPort);
});

// 注册 Config / Logger / MediaDbContext / 业务服务
AppBootstrap.ConfigureCoreServices(builder.Services, config, logger);

// 配置hangfire后台服务
// var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection");
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSerilogLogProvider()
    .UseMemoryStorage());
// .UseSQLiteStorage(hangfireConnectionString));

// 禁用Hangfire默认的自动重试，改用项目自定义重试逻辑
GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 0 });

// 添加Hangfire的服务器，配置多个优先级队列
// 获取识别任务的最大并发数配置
var identificationWorkers = config.Tasks?.MaxConcurrentIdentificationTasks ?? 5;

// 主服务器处理除 identification 以外的所有队列
builder.Services.AddHangfireServer(options =>
{
    // identification 队列独占给专用识别服务器处理（见下方第二个 AddHangfireServer）。
    // 如果此处包含 identification，主服务器的 ProcessorCount*2 个 worker 也会拉识别任务，
    // 会让 MaxConcurrentIdentificationTasks 的限制失效（实际并发 = 5 + ProcessorCount*2）。
    options.Queues = new[] { "critical", "high", "default", "low", "background" };

    // 设置工作线程数
    options.WorkerCount = Environment.ProcessorCount * 2;

    // 设置轮询间隔
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);

    // 设置服务器名称
    options.ServerName = $"{Environment.MachineName}:NineKgTools";
});

// 为 identification 队列添加专门的服务器，提供更精细的并发控制
builder.Services.AddHangfireServer(options =>
{
    // 只处理识别任务队列
    options.Queues = new[] { "identification" };

    // 使用配置的并发数
    options.WorkerCount = identificationWorkers;

    // 设置轮询间隔
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);

    // 设置服务器名称
    options.ServerName = $"{Environment.MachineName}:NineKgTools-Identification";
});

Log.Information("Hangfire 配置完成：主服务器 Worker={WorkerCount}（不含 identification 队列），识别专用服务器 Worker={IdentificationWorkers}",
    Environment.ProcessorCount * 2, identificationWorkers);
builder.Services.AddMvc();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(sp.GetService<NavigationManager>()!.BaseUri)
});

// 添加控制器（包括Core项目中的控制器）
builder.Services.AddControllers()
    .AddApplicationPart(typeof(NineKgTools.Core.Controllers.Auth.AuthController).Assembly);

// 添加项目相关服务（业务服务由 AppBootstrap.ConfigureCoreServices 注册，这里只补 Web 特有项）
builder.Services.AddHttpContextAccessor(); // 添加 HttpContextAccessor 支持本地/远程访问判断

// 添加认证服务
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<UserInitializationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, CookieAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore();

// 配置Cookie认证
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "NineKgTools.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/login";
        options.LogoutPath = "/api/auth/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(1); // 默认1天
        options.SlidingExpiration = true;

        // API请求未认证时返回401，而不是重定向到登录页
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

// 添加MudBlazor服务到容器
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    config.SnackbarConfiguration.PreventDuplicates = true;
    config.SnackbarConfiguration.NewestOnTop = false;
    config.SnackbarConfiguration.ShowCloseIcon = false;
    config.SnackbarConfiguration.VisibleStateDuration = 500;
    config.SnackbarConfiguration.HideTransitionDuration = 500;
    config.SnackbarConfiguration.ShowTransitionDuration = 500;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Health check 端点，用于 Docker / K8s / 反向代理探测存活
//   - /healthz       基础存活探针，无依赖检查，永远返回 Healthy（除非整个进程挂了）
//   - 想加更细的就绪探针（如 SQLite 可连）后续在这里 .AddCheck<...>()
builder.Services.AddHealthChecks();

var app = builder.Build();

// =====================================================================================================
// 配置 HTTP 请求管道
// =====================================================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// 配置静态文件中间件
app.UseStaticFiles();

app.UseAntiforgery();

// 认证和授权中间件
app.UseAuthentication();
app.UseAuthorization();

// 控制器路由必须在 Blazor 组件之前
app.MapControllers();

// 健康检查端点 — 必须可匿名访问（在 UseAuthentication 之后映射，但端点本身不要求 [Authorize]）
// 容器/反代/监控以 GET /healthz 探测；返回 "Healthy" + HTTP 200 即视为存活
app.MapHealthChecks("/healthz").AllowAnonymous();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 用于调试
app.UseHangfireDashboard();

// 数据库 schema 迁移 + Core 业务初始化（标签/分类/收藏夹/媒体源/向量库等）
// 全部分支逻辑见 NineKgTools.Core.Hosting.AppBootstrap.RunStartupAsync 与 MediaDbContextMigrator.EnsureSchemaAsync
await AppBootstrap.RunStartupAsync(app.Services);

// Web 端独有：初始化默认用户（认证服务）
using (var scope = app.Services.CreateScope())
{
    var userInitService = scope.ServiceProvider.GetRequiredService<UserInitializationService>();
    await userInitService.InitializeDefaultUserAsync();
}


app.Lifetime.ApplicationStarted.Register(async void () =>
{
    try
    {
        await AppBootstrap.RunAfterStartupAsync(app.Services);

        Log.Information("网页服务运行在 {WebHost}:{WebPort}", config.App.WebHost, config.App.WebPort);
        Log.Information("应用已启动...");
    }
    catch (Exception e)
    {
        Log.Error(e, "应用启动失败");
    }
});

app.Lifetime.ApplicationStopping.Register(() => AppBootstrap.ShutdownCleanup(app.Services));


app.Run();
