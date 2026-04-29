using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Websites;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace NineKgTools.Core.Services.Source;

/// <summary>
/// 媒体源服务，负责处理媒体源的增删改查
/// </summary>
public class SourceService
{
    private readonly WebsiteService _websiteService;
    private readonly MediaDbContext _dbContext;

    public SourceService(WebsiteService websiteService, MediaDbContext dbContext)
    {
        _websiteService = websiteService;
        _dbContext = dbContext;
    }

    /// <summary>
    /// 寻找数据库中的MediaSource
    /// </summary>
    public async Task<MediaSource?> FindMediaSourceAsync(MediaSource mediaSource)
    {
        return await _dbContext.MediaSources
            .Include(m => m.MediaBase)
            .SingleOrDefaultAsync(m => m.FullPath == mediaSource.FullPath);
    }

    /// <summary>
    /// 批量查询给定路径列表中已存在 MediaSource 记录的 path→Id 映射。
    /// 用于 SourcesPage 等场景判断哪些路径已经被识别过，决定是否显示"查看详情"按钮。
    /// 不在数据库的路径不会出现在结果字典中。
    /// </summary>
    public async Task<Dictionary<string, int>> GetIdsForPathsAsync(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();
        if (pathList.Count == 0)
            return new Dictionary<string, int>();

        var found = await _dbContext.MediaSources
            .Where(m => pathList.Contains(m.FullPath))
            .Select(m => new { m.FullPath, m.Id })
            .ToListAsync();

        return found.ToDictionary(x => x.FullPath, x => x.Id);
    }
    
    public async Task RemoveMediaSourceAsync(MediaSource mediaSource)
    {
        _dbContext.MediaSources.Remove(mediaSource);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<MediaSource> AddMediaSourceAsync(MediaSource mediaSource)
    {
        await _dbContext.MediaSources.AddAsync(mediaSource);
        await _dbContext.SaveChangesAsync();
        
        return mediaSource;
    }

    public async Task InitializeMediaSourcesDb()
    {
        Log.Information("正在初始化媒体源数据库...");

        StaticSources.MediaSources = await _dbContext.MediaSources.ToListAsync();
        Log.Information("媒体源数据库初始化完毕");
    }
}