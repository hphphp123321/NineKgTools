using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NineKgTools.Core.DbContexts;

/// <summary>
/// dotnet ef 设计期工厂。供 <c>dotnet ef migrations add</c> / <c>dotnet ef dbcontext info</c> 使用。
///
/// 不会读 <c>config.yaml</c>，避免设计期触发 Program.cs 启动副作用（日志、Hangfire、HTTP 端口绑定等）。
/// 用一个固定的占位连接字符串就够了——dotnet ef 只需要 model 元数据，不会真连库。
///
/// 团队成员加迁移时执行：
/// <code>
/// dotnet ef migrations add VersionLabel_Description ^
///   --project NineKgTools.Core ^
///   --startup-project NineKgTools.Core ^
///   --output-dir Core/DbContexts/Migrations
/// </code>
/// </summary>
public class MediaDbContextDesignFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<MediaDbContext>();
        builder.UseSqlite(
            "Data Source=design-time.db",
            o => o.MigrationsAssembly(typeof(MediaDbContext).Assembly.FullName));
        return new MediaDbContext(builder.Options);
    }
}
