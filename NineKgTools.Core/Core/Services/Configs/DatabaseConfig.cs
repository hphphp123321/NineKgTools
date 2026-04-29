using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class DatabaseConfig
{
    [YamlMember(Alias = "path", Description = "数据库文件保存路径")]
    public string Path { get; set; } = "Database/database.db";

    [YamlMember(Alias = "hangfire_path", Description = "Hangfire数据库文件保存路径")]
    public string HangfirePath { get; set; } = "Database/hangfire.db";

    /// <summary>
    /// 获取默认数据库的连接字符串
    /// </summary>
    public string GetConnectionString() => $"Data Source={Path}";

    /// <summary>
    /// 获取Hangfire数据库的连接字符串
    /// </summary>
    public string GetHangfireConnectionString() => $"Data Source={HangfirePath}";

    public DatabaseConfig Copy()
    {
        return new DatabaseConfig
        {
            Path = Path,
            HangfirePath = HangfirePath
        };
    }
}
