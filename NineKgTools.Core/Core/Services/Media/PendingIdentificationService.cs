using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using Serilog;

namespace NineKgTools.Core.Services.Media;

/// <summary>
/// 管理"已识别但尚未入库"的媒体识别结果。
/// 与正式的 Medias 表分离，避免污染全局媒体查询。
/// </summary>
public class PendingIdentificationService
{
    private readonly MediaDbContext _dbContext;

    public PendingIdentificationService(MediaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// 把识别结果暂存到 PendingIdentification 表。
    /// 如果该 MediaSource 已经有一条 pending 记录，则覆盖。
    /// </summary>
    public async Task<PendingIdentification> SaveAsync(MediaSource source, MediaBase media, CancellationToken ct = default)
    {
        if (source.Id <= 0)
            throw new InvalidOperationException("保存 PendingIdentification 前 MediaSource 必须已入库（Id > 0）");

        var json = MediaBaseJsonSerializer.Serialize(media);
        var typeName = media.GetType().Name;

        var existing = await _dbContext.PendingIdentifications
            .FirstOrDefaultAsync(p => p.MediaSourceId == source.Id, ct);

        if (existing != null)
        {
            existing.MediaTypeName = typeName;
            existing.MediaBaseJson = json;
            existing.IdentifiedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PendingIdentification
            {
                MediaSourceId = source.Id,
                MediaTypeName = typeName,
                MediaBaseJson = json,
                IdentifiedAt = DateTime.UtcNow
            };
            await _dbContext.PendingIdentifications.AddAsync(existing, ct);
        }

        await _dbContext.SaveChangesAsync(ct);
        return existing;
    }

    /// <summary>
    /// 读取指定 MediaSource 的 pending 识别结果并反序列化为 MediaBase 派生类实例。
    /// 反序列化后的 Source 已绑定到当前 MediaSource 实体（若提供）。
    /// </summary>
    public async Task<MediaBase?> LoadAsync(int mediaSourceId, CancellationToken ct = default)
    {
        var entry = await _dbContext.PendingIdentifications
            .Include(p => p.MediaSource)
            .FirstOrDefaultAsync(p => p.MediaSourceId == mediaSourceId, ct);

        if (entry == null)
            return null;

        return MediaBaseJsonSerializer.Deserialize(entry.MediaBaseJson, entry.MediaSource);
    }

    /// <summary>
    /// 删除指定 MediaSource 的 pending 识别结果。
    /// </summary>
    public async Task RemoveBySourceIdAsync(int mediaSourceId, CancellationToken ct = default)
    {
        var entry = await _dbContext.PendingIdentifications
            .FirstOrDefaultAsync(p => p.MediaSourceId == mediaSourceId, ct);

        if (entry != null)
        {
            _dbContext.PendingIdentifications.Remove(entry);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// 加载所有 pending 识别结果及其关联的 MediaSource，用于"待处理"页面的"待入库" Tab。
    /// 批量反序列化时使用同一 DbContext 加载 MediaSource 实体一次性获取。
    /// </summary>
    public async Task<List<(MediaSource source, MediaBase media)>> GetAllPendingAsync(CancellationToken ct = default)
    {
        var entries = await _dbContext.PendingIdentifications
            .Include(p => p.MediaSource)
            .OrderByDescending(p => p.IdentifiedAt)
            .ToListAsync(ct);

        var result = new List<(MediaSource, MediaBase)>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.MediaSource == null) continue;

            try
            {
                var media = MediaBaseJsonSerializer.Deserialize(entry.MediaBaseJson, entry.MediaSource);
                if (media != null)
                    result.Add((entry.MediaSource, media));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "反序列化 PendingIdentification 失败: MediaSourceId={Id}", entry.MediaSourceId);
            }
        }
        return result;
    }

    /// <summary>
    /// 统计 pending 记录数量（给统计卡片用）。
    /// </summary>
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await _dbContext.PendingIdentifications.CountAsync(ct);
    }

    /// <summary>
    /// 清理超过保留期限的 pending 识别结果（被 PendingIdentificationCleanupService 调用）。
    /// 同时把对应 MediaSource 的 Identified 置回 false，使其回到"待识别"状态。
    /// </summary>
    /// <param name="retentionDays">保留天数，0 表示永不清理</param>
    /// <returns>被清理的记录数</returns>
    public async Task<int> CleanupExpiredAsync(int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return 0;

        var threshold = DateTime.UtcNow.AddDays(-retentionDays);
        var expired = await _dbContext.PendingIdentifications
            .Include(p => p.MediaSource)
            .Where(p => p.IdentifiedAt < threshold)
            .ToListAsync(ct);

        if (expired.Count == 0) return 0;

        foreach (var entry in expired)
        {
            if (entry.MediaSource != null)
                entry.MediaSource.Identified = false;
            _dbContext.PendingIdentifications.Remove(entry);
        }

        await _dbContext.SaveChangesAsync(ct);
        Log.Information("清理超期 pending 识别结果: {Count} 条（保留天数={Days}）", expired.Count, retentionDays);
        return expired.Count;
    }
}
