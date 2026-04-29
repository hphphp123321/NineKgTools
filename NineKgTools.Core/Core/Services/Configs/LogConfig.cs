using Serilog.Events;
using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public enum LogType
{
    Console,
    File,
    Server
}

public class LogConfig
{
    [YamlMember(Alias = "log_types", Description = "日志输出类型, 可选值: Console, File, Server")]
    public IEnumerable<LogType> LogTypes { get; set; }
    
    [YamlMember(Alias = "log_path", Description = "日志文件路径")]
    public string LogPath { get; set; }
    
    [YamlMember(Alias = "log_server", Description = "日志服务器地址, 例如群晖默认为'群晖ip:514'")]
    public string LogServer { get; set; }
    
    [YamlMember(Alias = "log_level", Description = "日志级别, 可选值: Verbose, Debug, Information, Warning, Error, Fatal")]
    public LogEventLevel LogLevel { get; set; }
    
    [YamlMember(Alias = "log_template", Description = "日志模板")]
    public string LogTemplate { get; set; }

    public LogConfig Copy()
    {
        return new LogConfig
        {
            LogTypes = LogTypes,
            LogPath = LogPath,
            LogServer = LogServer,
            LogLevel = LogLevel,
            LogTemplate = LogTemplate
        };
    }
}