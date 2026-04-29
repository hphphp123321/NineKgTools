using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.DbContexts;

/// <summary>
/// SaveChanges 拦截器：在保存 Media 前自动同步 Creators 集合
/// 从子类的各个角色属性（Authors、Illustrators等）收集所有 Creator 到统一的 Creators 属性
/// </summary>
public class CreatorSyncInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        SyncAllMediaCreators(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SyncAllMediaCreators(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// 同步所有已修改或新增的 Media 实体的 Creators 集合
    /// </summary>
    private static void SyncAllMediaCreators(DbContext? context)
    {
        if (context == null) return;

        // 获取所有已修改或新增的 MediaBase 实体
        var mediaEntries = context.ChangeTracker.Entries<MediaBase>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        foreach (var entry in mediaEntries)
        {
            // 调用 SyncCreators 方法同步 Creators 集合
            entry.Entity.SyncCreators();
        }
    }
}
