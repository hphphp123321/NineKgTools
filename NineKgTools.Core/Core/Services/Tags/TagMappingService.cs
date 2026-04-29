using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tags;
using Serilog;

namespace NineKgTools.Core.Services.Tags;

/// <summary>
/// 标签映射服务实现
/// </summary>
public class TagMappingService
{
    private readonly MediaDbContext _context;
    
    public TagMappingService(MediaDbContext context)
    {
        _context = context;
    }
    
    /// <summary>
    /// 添加标签映射
    /// </summary>
    /// <param name="sourceName">源标签名称</param>
    /// <param name="targetTagId">目标标签ID</param>
    /// <param name="description">映射描述</param>
    /// <returns>创建的映射</returns>
    public async Task<TagMapping> AddMappingAsync(string sourceName, int targetTagId, string? description = null)
    {
        // 检查源名称是否已存在
        var existing = await _context.TagMappings
            .FirstOrDefaultAsync(tm => tm.SourceName == sourceName);
        
        if (existing != null)
        {
            throw new InvalidOperationException($"源标签名称 '{sourceName}' 已存在映射");
        }
        
        // 检查目标标签是否存在
        var targetTag = await _context.Tags.FindAsync(targetTagId);
        if (targetTag == null)
        {
            throw new ArgumentException($"目标标签ID {targetTagId} 不存在");
        }
        
        var mapping = new TagMapping
        {
            SourceName = sourceName,
            TargetTagId = targetTagId,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.TagMappings.Add(mapping);
        await _context.SaveChangesAsync();
        
        Log.Information("添加标签映射: {Source} -> {Target}", sourceName, targetTag.Name);
        
        return mapping;
    }
    
    /// <summary>
    /// 更新标签映射
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <param name="mapping">更新的映射信息</param>
    /// <returns>更新后的映射</returns>
    public async Task<TagMapping?> UpdateMappingAsync(int id, TagMapping mapping)
    {
        var existing = await _context.TagMappings.FindAsync(id);
        if (existing == null)
        {
            return null;
        }
        
        // 检查新的源名称是否与其他映射冲突
        if (existing.SourceName != mapping.SourceName)
        {
            var duplicate = await _context.TagMappings
                .AnyAsync(tm => tm.SourceName == mapping.SourceName && tm.Id != id);
            
            if (duplicate)
            {
                throw new InvalidOperationException($"源标签名称 '{mapping.SourceName}' 已被其他映射使用");
            }
        }
        
        // 如果目标标签ID变更，检查新的目标标签是否存在
        if (mapping.TargetTagId.HasValue && mapping.TargetTagId != existing.TargetTagId)
        {
            var targetExists = await _context.Tags.AnyAsync(t => t.Id == mapping.TargetTagId);
            if (!targetExists)
            {
                throw new ArgumentException($"目标标签ID {mapping.TargetTagId} 不存在");
            }
        }
        
        existing.SourceName = mapping.SourceName;
        existing.TargetTagId = mapping.TargetTagId;
        existing.Description = mapping.Description;
        existing.IsActive = mapping.IsActive;
        existing.Priority = mapping.Priority;
        existing.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        Log.Information("更新标签映射 ID {Id}: {Source}", id, mapping.SourceName);
        
        return existing;
    }
    
    /// <summary>
    /// 删除标签映射
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <returns>是否删除成功</returns>
    public async Task<bool> DeleteMappingAsync(int id)
    {
        var mapping = await _context.TagMappings.FindAsync(id);
        if (mapping == null)
        {
            return false;
        }
        
        _context.TagMappings.Remove(mapping);
        await _context.SaveChangesAsync();
        
        Log.Information("删除标签映射 ID {Id}: {Source}", id, mapping.SourceName);
        
        return true;
    }
    
    /// <summary>
    /// 根据ID获取映射
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <returns>映射信息</returns>
    public async Task<TagMapping?> GetMappingByIdAsync(int id)
    {
        return await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .ThenInclude(t => t!.TopTag)
            .FirstOrDefaultAsync(tm => tm.Id == id);
    }
    
    /// <summary>
    /// 根据源名称获取映射
    /// </summary>
    /// <param name="sourceName">源标签名称</param>
    /// <returns>映射信息</returns>
    public async Task<TagMapping?> GetMappingBySourceAsync(string sourceName)
    {
        return await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .ThenInclude(t => t!.TopTag)
            .FirstOrDefaultAsync(tm => tm.SourceName == sourceName && tm.IsActive);
    }
    
    /// <summary>
    /// 获取所有映射
    /// </summary>
    /// <param name="isActive">筛选是否启用的映射，null表示获取全部</param>
    /// <param name="includeTargetTag">是否包含目标标签信息</param>
    /// <returns>映射列表</returns>
    public async Task<List<TagMapping>> GetAllMappingsAsync(bool? isActive = null, bool includeTargetTag = true)
    {
        var query = _context.TagMappings.AsQueryable();
        
        if (isActive.HasValue)
        {
            query = query.Where(tm => tm.IsActive == isActive.Value);
        }
        
        if (includeTargetTag)
        {
            query = query.Include(tm => tm.TargetTag)
                .ThenInclude(t => t!.TopTag);
        }
        
        return await query
            .OrderBy(tm => tm.Priority)
            .ThenBy(tm => tm.SourceName)
            .ToListAsync();
    }
    
    /// <summary>
    /// 获取按优先级排序的活动映射
    /// </summary>
    /// <param name="includeTargetTag">是否包含目标标签信息</param>
    /// <returns>映射列表</returns>
    public async Task<List<TagMapping>> GetActiveMappingsOrderedAsync(bool includeTargetTag = true)
    {
        var query = _context.TagMappings
            .Where(tm => tm.IsActive);
        
        if (includeTargetTag)
        {
            query = query.Include(tm => tm.TargetTag)
                .ThenInclude(t => t!.TopTag);
        }
        
        return await query
            .OrderBy(tm => tm.Priority)
            .ThenBy(tm => tm.SourceName)
            .ToListAsync();
    }
    
    /// <summary>
    /// 切换映射启用状态
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <returns>更新后的映射</returns>
    public async Task<TagMapping?> ToggleMappingStatusAsync(int id)
    {
        var mapping = await _context.TagMappings.FindAsync(id);
        if (mapping == null)
        {
            return null;
        }
        
        mapping.IsActive = !mapping.IsActive;
        mapping.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        Log.Information("切换标签映射状态 ID {Id}: IsActive = {IsActive}", id, mapping.IsActive);
        
        return mapping;
    }
    
    /// <summary>
    /// 更新映射优先级
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <param name="priority">新的优先级</param>
    /// <returns>更新后的映射</returns>
    public async Task<TagMapping?> UpdateMappingPriorityAsync(int id, int priority)
    {
        var mapping = await _context.TagMappings.FindAsync(id);
        if (mapping == null)
        {
            return null;
        }
        
        mapping.Priority = priority;
        mapping.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        Log.Information("更新标签映射优先级 ID {Id}: Priority = {Priority}", id, priority);
        
        return mapping;
    }
    
    /// <summary>
    /// 批量导入映射
    /// </summary>
    /// <param name="mappings">映射列表</param>
    /// <param name="skipExisting">是否跳过已存在的映射</param>
    /// <returns>成功导入的数量</returns>
    public async Task<int> ImportMappingsAsync(List<TagMapping> mappings, bool skipExisting = true)
    {
        int imported = 0;
        
        foreach (var mapping in mappings)
        {
            // 检查是否已存在
            var exists = await _context.TagMappings
                .AnyAsync(tm => tm.SourceName == mapping.SourceName);
            
            if (exists && skipExisting)
            {
                continue;
            }
            
            if (exists && !skipExisting)
            {
                // 更新现有映射
                var existing = await _context.TagMappings
                    .FirstAsync(tm => tm.SourceName == mapping.SourceName);
                
                existing.TargetTagId = mapping.TargetTagId;
                existing.Description = mapping.Description;
                existing.IsActive = mapping.IsActive;
                existing.Priority = mapping.Priority;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // 添加新映射
                var newMapping = new TagMapping
                {
                    SourceName = mapping.SourceName,
                    TargetTagId = mapping.TargetTagId,
                    Description = mapping.Description,
                    IsActive = mapping.IsActive,
                    Priority = mapping.Priority,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.TagMappings.Add(newMapping);
            }
            
            imported++;
        }
        
        await _context.SaveChangesAsync();
        
        Log.Information("导入 {Count} 个标签映射", imported);
        
        return imported;
    }
    
    /// <summary>
    /// 导出所有映射
    /// </summary>
    /// <param name="isActive">筛选是否启用的映射，null表示导出全部</param>
    /// <returns>映射列表</returns>
    public async Task<List<TagMapping>> ExportMappingsAsync(bool? isActive = null)
    {
        return await GetAllMappingsAsync(isActive, true);
    }
    
    /// <summary>
    /// 记录映射命中（增加命中次数）
    /// </summary>
    /// <param name="id">映射ID</param>
    public async Task RecordMappingHitAsync(int id)
    {
        var mapping = await _context.TagMappings.FindAsync(id);
        if (mapping == null)
        {
            return;
        }
        
        mapping.HitCount++;
        mapping.LastHitAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        Log.Debug("记录标签映射命中 ID {Id}: HitCount = {HitCount}", id, mapping.HitCount);
    }
    
    /// <summary>
    /// 获取映射统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    public async Task<TagMappingStatistics> GetStatisticsAsync()
    {
        var mappings = await _context.TagMappings.ToListAsync();
        
        var stats = new TagMappingStatistics
        {
            TotalMappings = mappings.Count,
            ActiveMappings = mappings.Count(m => m.IsActive),
            InactiveMappings = mappings.Count(m => !m.IsActive),
            TotalHits = mappings.Sum(m => m.HitCount),
            UnusedMappings = mappings.Count(m => m.HitCount == 0)
        };
        
        // 获取最常用的映射（前10个）
        stats.MostUsedMappings = await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .OrderByDescending(tm => tm.HitCount)
            .Take(10)
            .ToListAsync();
        
        // 获取最近使用的映射（前10个）
        stats.RecentlyUsedMappings = await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .Where(tm => tm.LastHitAt.HasValue)
            .OrderByDescending(tm => tm.LastHitAt)
            .Take(10)
            .ToListAsync();
        
        return stats;
    }
    
    /// <summary>
    /// 清理未使用的映射（长时间未命中的）
    /// </summary>
    /// <param name="daysSinceLastHit">多少天未命中</param>
    /// <returns>清理的数量</returns>
    public async Task<int> CleanupUnusedMappingsAsync(int daysSinceLastHit = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysSinceLastHit);
        
        var unusedMappings = await _context.TagMappings
            .Where(tm => tm.HitCount == 0 || 
                        (tm.LastHitAt.HasValue && tm.LastHitAt.Value < cutoffDate))
            .ToListAsync();
        
        _context.TagMappings.RemoveRange(unusedMappings);
        await _context.SaveChangesAsync();
        
        Log.Information("清理 {Count} 个未使用的标签映射", unusedMappings.Count);
        
        return unusedMappings.Count;
    }
    
    /// <summary>
    /// 验证映射是否有效（目标标签是否存在）
    /// </summary>
    /// <param name="id">映射ID</param>
    /// <returns>是否有效</returns>
    public async Task<bool> ValidateMappingAsync(int id)
    {
        var mapping = await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .FirstOrDefaultAsync(tm => tm.Id == id);
        
        if (mapping == null)
        {
            return false;
        }
        
        // 检查目标标签是否存在
        if (mapping.TargetTagId.HasValue)
        {
            return mapping.TargetTag != null;
        }
        
        return true;
    }
    
    /// <summary>
    /// 批量验证所有映射
    /// </summary>
    /// <returns>无效映射的ID列表</returns>
    public async Task<List<int>> ValidateAllMappingsAsync()
    {
        var invalidMappings = await _context.TagMappings
            .Include(tm => tm.TargetTag)
            .Where(tm => tm.TargetTagId.HasValue && tm.TargetTag == null)
            .Select(tm => tm.Id)
            .ToListAsync();
        
        if (invalidMappings.Any())
        {
            Log.Warning("发现 {Count} 个无效的标签映射", invalidMappings.Count);
        }
        
        return invalidMappings;
    }
}

/// <summary>
/// 标签映射统计信息
/// </summary>
public class TagMappingStatistics
{
    /// <summary>
    /// 总映射数
    /// </summary>
    public int TotalMappings { get; set; }
    
    /// <summary>
    /// 活动映射数
    /// </summary>
    public int ActiveMappings { get; set; }
    
    /// <summary>
    /// 非活动映射数
    /// </summary>
    public int InactiveMappings { get; set; }
    
    /// <summary>
    /// 总命中次数
    /// </summary>
    public int TotalHits { get; set; }
    
    /// <summary>
    /// 最常用的映射
    /// </summary>
    public List<TagMapping> MostUsedMappings { get; set; } = new();
    
    /// <summary>
    /// 最近使用的映射
    /// </summary>
    public List<TagMapping> RecentlyUsedMappings { get; set; } = new();
    
    /// <summary>
    /// 从未使用的映射数
    /// </summary>
    public int UnusedMappings { get; set; }
}