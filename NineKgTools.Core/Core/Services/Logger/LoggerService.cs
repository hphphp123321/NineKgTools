using NineKgTools.Core.Services.Configs;
using Serilog;
using Serilog.Enrichers.CallerInfo;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace NineKgTools.Core.Services.Logger;

public class LoggerService
{
    private readonly LogConfig _config;

    public LoggerService(Config config)
    {
        _config = config.Log;
    }

    public void ConfigureLogger()
    {
        Log.CloseAndFlush();
        var loggerConfiguration = ConfigureLogging();
        Log.Logger = loggerConfiguration.CreateLogger();
    }

    /// 配置日志输出
    private LoggerConfiguration ConfigureLogging()
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Is(_config.LogLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning) // 仅记录来自'Microsoft'命名空间的警告或更高级别的日志
            .MinimumLevel.Override("System", LogEventLevel.Warning) // 同上，适用于'System'命名空间
            .MinimumLevel.Override("Hangfire", LogEventLevel.Information) // 同上，适用于'Hangfire'命名空间
            .MinimumLevel.Override("MudBlazor", LogEventLevel.Information) // 同上，适用于'Microsoft.Hosting.Lifetime'命名空间
            .Enrich.WithCallerInfo(
                includeFileInfo: true,
                assemblyPrefix: "NineKgTools",
                filePathDepth: 1
                );


        if (_config.LogTypes.Contains(LogType.Console))
        {
            // 容器环境下用 JSON 单行格式，便于 Loki / Promtail / docker logs --format 解析
            // 检测 .NET 官方约定的 DOTNET_RUNNING_IN_CONTAINER（aspnet 镜像默认设为 true）
            var isContainer = string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            if (isContainer)
            {
                loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
            }
            else
            {
                loggerConfiguration.WriteTo.Console(outputTemplate: _config.LogTemplate);
            }
        }

        if (_config.LogTypes.Contains(LogType.File))
        {
            if (!Path.Exists(_config.LogPath))
            {
                // 创建日志文件夹
                Directory.CreateDirectory(_config.LogPath);
            }

            loggerConfiguration.WriteTo.File(
                Path.Combine(_config.LogPath, "log-.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: _config.LogTemplate);
        }

        // 仅在 LogServer 真有值时才注册 Syslog sink。
        // 否则空字符串会让 UdpSyslog 持续尝试解析空 host 失败，污染日志且无意义。
        // 用户在 Docker 中默认 LogServer 为空，避免内网地址硬编码进镜像。
        if (_config.LogTypes.Contains(LogType.Server) && !string.IsNullOrWhiteSpace(_config.LogServer))
        {
            var (logServerHost, logServerPort) = ParseLogServer(_config.LogServer);

            loggerConfiguration.WriteTo.UdpSyslog(
                logServerHost,
                logServerPort,
                appName: "9KgTools",
                outputTemplate: _config.LogTemplate);
        }

        return loggerConfiguration;
    }

    private static (string Host, int Port) ParseLogServer(string logServer)
    {
        var parts = logServer.Split(':');
        var host = parts[0];
        var port = parts.Length > 1 && int.TryParse(parts[1], out var parsedPort) ? parsedPort : 514; // 默认端口 514

        return (host, port);
    }

}
