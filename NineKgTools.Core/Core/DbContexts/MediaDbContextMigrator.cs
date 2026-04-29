using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Serilog;

namespace NineKgTools.Core.DbContexts;

/// <summary>
/// 启动期数据库 schema 协调器。统一处理"已有 EnsureCreated 旧库"与"基于 EF Migrations 的版本演进"。
///
/// <para>四路分支（按优先级）：</para>
/// <list type="number">
/// <item><b>NINEKG_RESET_DB=true</b>：先 EnsureDeletedAsync 删库，再走"库不存在"分支。仅开发期使用。</item>
/// <item><b>库不存在</b>（CanConnectAsync=false）：EnsureCreatedAsync 首次建库 +
/// 把全部已知 migrations 标记为已应用（避免下次启动以为有 pending）。</item>
/// <item><b>库存在但无 __EFMigrationsHistory 表</b>：判定为旧版 EnsureCreated 创建的库。
/// 假设其 schema 等价于"应用了已发布的全部 migrations 之后"，盖 baseline——
/// 创建 history 表 + 把全部 migrations 写进去——之后启动会按正常路径只跑新增的 migrations。</item>
/// <item><b>库存在且 history 完整</b>：MigrateAsync 应用 pending migrations。</item>
/// </list>
///
/// <para><b>NINEKG_DB_AUTO_MIGRATE=false</b>：在第 4 路上禁用自动 Migrate（仅记日志，方便生产手动选时机升级）。
/// 第 1/2/3 路不受此变量影响——首次建库与 baseline 是必须做的。</para>
///
/// <para><b>引入第一个 Migration 的注意事项</b>：
/// 团队首次执行 <c>dotnet ef migrations add InitialCreate</c> 之后，
/// 现网旧库（无 history）启动时会走分支 3，把 InitialCreate 直接 baseline 进 history 而不重新执行其 SQL——
/// 因为旧库的表已经被 EnsureCreated 建过了。<b>前提</b>：InitialCreate 生成的 schema 与当前 EnsureCreated 的运行结果一致。
/// 具体校验方法见 docs/operations/deployment.md "数据库迁移" 章节。</para>
/// </summary>
public static class MediaDbContextMigrator
{
    public enum MigrationOutcome
    {
        /// <summary>库被删除并重建（NINEKG_RESET_DB=true）</summary>
        DatabaseReset,

        /// <summary>库原本不存在，刚 EnsureCreated 完毕</summary>
        DatabaseCreated,

        /// <summary>库已存在但缺 history，已盖 baseline</summary>
        Baselined,

        /// <summary>跑了 pending 迁移</summary>
        Migrated,

        /// <summary>库已是最新，无操作</summary>
        AlreadyUpToDate,

        /// <summary>跳过（NINEKG_DB_AUTO_MIGRATE=false 且无需建库/baseline）</summary>
        Skipped,
    }

    public static async Task<MigrationOutcome> EnsureSchemaAsync(MediaDbContext db, CancellationToken ct = default)
    {
        var resetDb = string.Equals(
            Environment.GetEnvironmentVariable("NINEKG_RESET_DB"),
            "true",
            StringComparison.OrdinalIgnoreCase);
        var autoMigrate = !string.Equals(
            Environment.GetEnvironmentVariable("NINEKG_DB_AUTO_MIGRATE"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        var wasReset = false;
        if (resetDb)
        {
            Log.Warning("检测到环境变量 NINEKG_RESET_DB=true，删除并重建数据库（清空全部数据）");
            await db.Database.EnsureDeletedAsync(ct);
            wasReset = true;
        }

        // 分支 1+2：库不存在 → EnsureCreated + 把全部已知 migrations 标为已应用
        if (!await db.Database.CanConnectAsync(ct))
        {
            Log.Information("数据库不存在，执行 EnsureCreatedAsync 首次建库");
            await db.Database.EnsureCreatedAsync(ct);
            await StampHistoryWithAllMigrationsAsync(db, ct);
            return wasReset ? MigrationOutcome.DatabaseReset : MigrationOutcome.DatabaseCreated;
        }

        var historyExists = await HistoryTableExistsAsync(db, ct);
        var allMigrations = db.Database.GetMigrations().ToList();

        // 分支 3：库存在但缺 history → baseline
        if (!historyExists)
        {
            if (allMigrations.Count == 0)
            {
                Log.Debug("库存在、无 Migrations、无 history，跳过（项目尚未引入 EF Migrations）");
                return MigrationOutcome.Skipped;
            }

            Log.Warning(
                "检测到旧库（EnsureCreated 创建，无 __EFMigrationsHistory），baseline 全部 {Count} 个已知 migrations。" +
                "确认现有 schema 与首个 InitialCreate migration 一致后再继续；不一致请清库重建。",
                allMigrations.Count);
            await StampHistoryWithAllMigrationsAsync(db, ct);
            return MigrationOutcome.Baselined;
        }

        // 分支 4：库存在且 history 完整
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count == 0)
        {
            Log.Debug("数据库已是最新，无待执行迁移");
            return MigrationOutcome.AlreadyUpToDate;
        }

        if (!autoMigrate)
        {
            Log.Warning(
                "检测到 {Count} 个待执行迁移但 NINEKG_DB_AUTO_MIGRATE=false，已跳过：{Migrations}",
                pending.Count, string.Join(", ", pending));
            return MigrationOutcome.Skipped;
        }

        Log.Information("应用 {Count} 个待执行迁移：{Migrations}",
            pending.Count, string.Join(", ", pending));
        await db.Database.MigrateAsync(ct);
        return MigrationOutcome.Migrated;
    }

    /// <summary>查 sqlite_master 看 __EFMigrationsHistory 是否存在。</summary>
    private static async Task<bool> HistoryTableExistsAsync(MediaDbContext db, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var shouldClose = false;
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            shouldClose = true;
        }
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
            var result = await cmd.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await conn.CloseAsync();
            }
        }
    }

    /// <summary>
    /// 创建 __EFMigrationsHistory（如不存在）+ 把全部已知 migrations 写入。
    /// 用 EF 内部 IHistoryRepository 拼 SQL，比手写更稳——schema 和插入语义跟 EF 同步。
    /// </summary>
    private static async Task StampHistoryWithAllMigrationsAsync(MediaDbContext db, CancellationToken ct)
    {
        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            Log.Debug("Assembly 中无 migrations，跳过 history 标记");
            return;
        }

        var historyRepo = db.Database.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(historyRepo.GetCreateIfNotExistsScript(), ct);

        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "9.0.0";
        foreach (var migrationId in migrations)
        {
            await db.Database.ExecuteSqlRawAsync(
                historyRepo.GetInsertScript(new HistoryRow(migrationId, productVersion)),
                ct);
        }
        Log.Information("已写入 {Count} 个 migration 标记到 __EFMigrationsHistory", migrations.Count);
    }
}
