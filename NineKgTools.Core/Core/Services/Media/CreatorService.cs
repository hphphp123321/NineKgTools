using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Images;
using Microsoft.EntityFrameworkCore;
using X.PagedList;
using X.PagedList.Extensions;

namespace NineKgTools.Core.Services.Media;

public class CreatorService
{
    private readonly MediaDbContext _dbContext;
    private readonly ImageService _imageService;

    public CreatorService(MediaDbContext dbContext, ImageService imageService)
    {
        _dbContext = dbContext;
        _imageService = imageService;
    }

    public async Task<Circle?> GetCircleAsync(int circleId)
    {
        return await _dbContext.Circles
            .Include(c => c.Avatar)
            .Include(c => c.Medias)
            .ThenInclude(m => m.Poster)
            .FirstOrDefaultAsync(c => c.Id == circleId);
    }

    public async Task FindAndUpdateCircleAsync(Circle circle)
    {
        var dbCircle = await _dbContext.Circles.FindAsync(circle.Id);
        if (dbCircle == null)
        {
            return;
        }

        // 保存并缓存社团图片
        if (circle.Avatar != null)
        {
            circle.Avatar = await _imageService.AddOrFindImageAsync(circle.Avatar, circle.Name);
        }

        dbCircle.Update(circle);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 获取或者插入一个GameCircle
    /// </summary>
    /// <returns></returns>
    public async Task<Circle> AddOrUpdateCircle(Circle circle)
    {
        // 首先尝试通过名称查找
        var dbGameCircle = await _dbContext.Circles
            .Include(c => c.Avatar)
            .FirstOrDefaultAsync(c => c.Name == circle.Name);
        
        // 如果通过名称没找到，且circle有别名，则尝试通过别名查找
        if (dbGameCircle == null && circle.AliasNames.Any())
        {
            // 获取所有Circle，然后在内存中进行别名匹配
            var allCircles = await _dbContext.Circles
                .Include(c => c.Avatar)
                .ToListAsync();
            
            dbGameCircle = allCircles.FirstOrDefault(c => 
                c.AliasNames.Any(alias => circle.AliasNames.Contains(alias) && !string.IsNullOrEmpty(alias)));
        }
        
        if (dbGameCircle != null)
        {
            dbGameCircle.Update(circle);
            // 保存并缓存图片
            var dbAvatar = await _imageService.AddOrFindImageAsync(circle.Avatar, circle.Name);
            if (dbAvatar != null)
            {
                dbGameCircle.Avatar = dbAvatar;
            }
            
            circle = dbGameCircle;
        }
        else
        {
            // 保存并缓存图片
            var dbAvatar = await _imageService.AddOrFindImageAsync(circle.Avatar, circle.Name);
            if (dbAvatar != null)
            {
                circle.Avatar = dbAvatar;
            }
            // 创建插入新的社团
            await _dbContext.Circles.AddAsync(circle);
        }

        await _dbContext.SaveChangesAsync();
        return circle;
    }

    public async Task<List<Circle>> AddOrUpdateCircles(List<Circle> circles)
    {
        var dbCircles = new List<Circle>();
        foreach (var circle in circles)
        {
            var dbCircle = await AddOrUpdateCircle(circle);
            dbCircles.Add(dbCircle);
        }

        return dbCircles.ToList();
    }

    /// <summary>
    /// 获取或者插入一个Creator
    /// </summary>
    /// <returns>与数据库数据一致的Creator</returns>
    public async Task<Creator> AddOrUpdateCreator(Creator creator)
    {
        // 首先尝试通过名称查找
        var dbGameCreator = await _dbContext.Creators
            .Include(c => c.Avatar)
            .FirstOrDefaultAsync(c => creator.Name == c.Name && !string.IsNullOrEmpty(creator.Name));

        // 如果通过名称没找到，且creator有别名，则尝试通过别名查找
        if (dbGameCreator == null && creator.AliasNames.Any())
        {
            // 获取所有Creator，然后在内存中进行别名匹配
            var allCreators = await _dbContext.Creators
                .Include(c => c.Avatar)
                .ToListAsync();
            
            dbGameCreator = allCreators.FirstOrDefault(c => 
                c.AliasNames.Any(alias => creator.AliasNames.Contains(alias) && !string.IsNullOrEmpty(alias)));
        }

        if (dbGameCreator != null)
        {
            dbGameCreator.Update(creator);
            // 保存并缓存图片
            var dbAvatar = await _imageService.AddOrFindImageAsync(creator.Avatar, creator.Name);
            if (dbAvatar != null)
            {
                dbGameCreator.Avatar = dbAvatar;
            }

            creator = dbGameCreator;
        }
        else
        {
            // 保存并缓存图片
            var dbAvatar = await _imageService.AddOrFindImageAsync(creator.Avatar, creator.Name);
            if (dbAvatar != null)
            {
                creator.Avatar = dbAvatar;
            }
            // 创建插入新的Creator
            await _dbContext.Creators.AddAsync(creator);
        }

        await _dbContext.SaveChangesAsync();
        return creator;
    }

    public async Task<List<Creator>> AddOrUpdateCreators(List<Creator> creators)
    {
        var dbCreators = new List<Creator>();
        foreach (var creator in creators)
        {
            var dbCreator = await AddOrUpdateCreator(creator);
            dbCreators.Add(dbCreator);
        }

        return dbCreators;
    }

    /// <summary>
    /// 根据ID获取Creator信息
    /// </summary>
    /// <param name="creatorId">Creator的ID</param>
    /// <returns>Creator信息，包含头像</returns>
    public async Task<Creator?> GetCreatorAsync(int creatorId)
    {
        return await _dbContext.Creators
            .Include(c => c.Avatar)
            .FirstOrDefaultAsync(c => c.Id == creatorId);
    }

    /// <summary>
    /// 查找并更新Creator信息
    /// </summary>
    /// <param name="creator">要更新的Creator信息</param>
    public async Task FindAndUpdateCreatorAsync(Creator creator)
    {
        var dbCreator = await _dbContext.Creators.FindAsync(creator.Id);
        if (dbCreator == null)
        {
            return;
        }

        // 保存并缓存人物图片
        if (creator.Avatar != null)
        {
            creator.Avatar = await _imageService.AddOrFindImageAsync(creator.Avatar, creator.Name);
        }

        dbCreator.Update(creator);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 获取Creator关联的所有媒体作品
    /// 使用统一的 Creator-Media 多对多关系进行查询
    /// </summary>
    /// <param name="creatorId">Creator的ID</param>
    /// <returns>关联的媒体作品列表，按发售日期降序排列</returns>
    public async Task<List<MediaBase>> GetCreatorMediasAsync(int creatorId)
    {
        // 通过新的统一多对多关系查询，一次查询即可获取所有关联媒体
        var creator = await _dbContext.Creators
            .AsNoTracking()
            .Include(c => c.Medias)
                .ThenInclude(m => m.Poster)
            .Include(c => c.Medias)
                .ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(c => c.Id == creatorId);

        if (creator == null || creator.Medias == null)
        {
            return new List<MediaBase>();
        }

        // 按发售日期降序排列
        return creator.Medias
            .OrderByDescending(m => m.ReleaseDate)
            .ToList();
    }

    /// <summary>
    /// 获取所有Creator
    /// </summary>
    /// <returns>所有Creator列表</returns>
    public async Task<List<Creator>> GetAllCreatorsAsync()
    {
        return await _dbContext.Creators
            .AsNoTracking()
            .Include(c => c.Avatar)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// 搜索Creator (按名称和别名)
    /// </summary>
    /// <param name="searchTerm">搜索关键词</param>
    /// <param name="maxResults">最大结果数</param>
    /// <returns>匹配的Creator列表</returns>
    public async Task<List<Creator>> SearchCreatorsByNameAsync(string searchTerm, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllCreatorsAsync();
        }

        var allCreators = await _dbContext.Creators
            .AsNoTracking()
            .Include(c => c.Avatar)
            .ToListAsync();

        // 在内存中进行名称和别名的模糊匹配
        return allCreators
            .Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.AliasNames.Any(alias => alias.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Name)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// 创建新Creator (仅需名称)
    /// </summary>
    /// <param name="name">Creator名称</param>
    /// <param name="types">Creator类型列表</param>
    /// <returns>创建的Creator</returns>
    public async Task<Creator> CreateCreatorAsync(string name, List<CreatorType>? types = null)
    {
        var creator = new Creator
        {
            Name = name,
            Types = types ?? new List<CreatorType>()
        };

        await _dbContext.Creators.AddAsync(creator);
        await _dbContext.SaveChangesAsync();

        return creator;
    }

    /// <summary>
    /// 更新Creator关联的媒体作品列表（直接管理多对多关联表，不触发SyncCreators拦截器）
    /// </summary>
    public async Task UpdateCreatorMediasAsync(int creatorId, List<int> mediaIds)
    {
        var creator = await _dbContext.Creators
            .Include(c => c.Medias)
            .FirstOrDefaultAsync(c => c.Id == creatorId);

        if (creator == null) return;

        var toAdd = mediaIds.Where(id => creator.Medias.All(m => m.Id != id)).ToList();
        var toRemove = creator.Medias.Where(m => !mediaIds.Contains(m.Id)).ToList();

        foreach (var id in toAdd)
        {
            var media = await _dbContext.Medias.FindAsync(id);
            if (media != null)
                creator.Medias.Add(media);
        }

        foreach (var media in toRemove)
        {
            creator.Medias.Remove(media);
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 获取Creator总数
    /// </summary>
    /// <returns>Creator总数</returns>
    public async Task<int> GetCreatorCountAsync()
    {
        return await _dbContext.Creators.CountAsync();
    }

    /// <summary>
    /// 分页查询 Creator，支持搜索词 + 类型筛选。
    /// 搜索涉及别名匹配（JSON 列存储），无法纯 SQL 完成，
    /// 因此先在内存中做模糊匹配再用 X.PagedList 截取页面。
    /// </summary>
    public async Task<IPagedList<Creator>> GetPagedCreatorsAsync(
        int pageNumber, int pageSize,
        string? searchTerm = null,
        CreatorType? filterType = null)
    {
        var allCreators = await _dbContext.Creators
            .AsNoTracking()
            .Include(c => c.Avatar)
            .ToListAsync();

        IEnumerable<Creator> filtered = allCreators;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filtered = filtered.Where(c =>
                c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.AliasNames.Any(alias => alias.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        if (filterType.HasValue)
        {
            filtered = filtered.Where(c => c.Types.Contains(filterType.Value));
        }

        return filtered.OrderBy(c => c.Name).ToPagedList(pageNumber, pageSize);
    }

    /// <summary>
    /// 按类型筛选Creator
    /// </summary>
    /// <param name="type">Creator类型</param>
    /// <returns>匹配类型的Creator列表</returns>
    public async Task<List<Creator>> GetCreatorsByTypeAsync(CreatorType type)
    {
        var allCreators = await _dbContext.Creators
            .AsNoTracking()
            .Include(c => c.Avatar)
            .ToListAsync();

        return allCreators
            .Where(c => c.Types.Contains(type))
            .OrderBy(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// 删除Creator
    /// </summary>
    /// <param name="creatorId">Creator的ID</param>
    public async Task DeleteCreatorAsync(int creatorId)
    {
        var creator = await _dbContext.Creators.FindAsync(creatorId);
        if (creator != null)
        {
            _dbContext.Creators.Remove(creator);
            await _dbContext.SaveChangesAsync();
        }
    }

    // ===== Circle 管理方法 =====

    /// <summary>
    /// 获取所有 Circle
    /// </summary>
    public async Task<List<Circle>> GetAllCirclesAsync()
    {
        return await _dbContext.Circles
            .AsNoTracking()
            .Include(c => c.Avatar)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    /// <summary>
    /// 按名称/别名搜索 Circle
    /// </summary>
    public async Task<List<Circle>> SearchCirclesByNameAsync(string searchTerm, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllCirclesAsync();
        }

        var allCircles = await _dbContext.Circles
            .AsNoTracking()
            .Include(c => c.Avatar)
            .ToListAsync();

        return allCircles
            .Where(c => c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        c.AliasNames.Any(alias => alias.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(c => c.Name)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// 创建新 Circle（仅需名称）
    /// </summary>
    public async Task<Circle> CreateCircleAsync(string name)
    {
        var circle = new Circle { Name = name };
        await _dbContext.Circles.AddAsync(circle);
        await _dbContext.SaveChangesAsync();
        return circle;
    }

    /// <summary>
    /// 获取 Circle 总数
    /// </summary>
    public async Task<int> GetCircleCountAsync()
    {
        return await _dbContext.Circles.CountAsync();
    }

    /// <summary>
    /// 分页查询 Circle，支持搜索词。别名匹配走内存，和 Creator 同一模式。
    /// </summary>
    public async Task<IPagedList<Circle>> GetPagedCirclesAsync(
        int pageNumber, int pageSize,
        string? searchTerm = null)
    {
        var allCircles = await _dbContext.Circles
            .AsNoTracking()
            .Include(c => c.Avatar)
            .ToListAsync();

        IEnumerable<Circle> filtered = allCircles;

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            filtered = filtered.Where(c =>
                c.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                c.AliasNames.Any(alias => alias.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
        }

        return filtered.OrderBy(c => c.Name).ToPagedList(pageNumber, pageSize);
    }

    /// <summary>
    /// 删除 Circle
    /// </summary>
    public async Task DeleteCircleAsync(int circleId)
    {
        var circle = await _dbContext.Circles.FindAsync(circleId);
        if (circle != null)
        {
            _dbContext.Circles.Remove(circle);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 获取 Circle 关联的所有媒体作品
    /// </summary>
    public async Task<List<MediaBase>> GetCircleMediasAsync(int circleId)
    {
        var circle = await _dbContext.Circles
            .AsNoTracking()
            .Include(c => c.Medias)
                .ThenInclude(m => m.Poster)
            .Include(c => c.Medias)
                .ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(c => c.Id == circleId);

        if (circle == null || circle.Medias == null)
        {
            return new List<MediaBase>();
        }

        return circle.Medias
            .OrderByDescending(m => m.ReleaseDate)
            .ToList();
    }

    /// <summary>
    /// 更新 Circle 关联媒体作品列表
    /// </summary>
    public async Task UpdateCircleMediasAsync(int circleId, List<int> mediaIds)
    {
        var circle = await _dbContext.Circles
            .Include(c => c.Medias)
            .FirstOrDefaultAsync(c => c.Id == circleId);

        if (circle == null) return;

        var toAdd = mediaIds.Where(id => circle.Medias.All(m => m.Id != id)).ToList();
        var toRemove = circle.Medias.Where(m => !mediaIds.Contains(m.Id)).ToList();

        foreach (var id in toAdd)
        {
            var media = await _dbContext.Medias.FindAsync(id);
            if (media != null)
                circle.Medias.Add(media);
        }

        foreach (var media in toRemove)
        {
            circle.Medias.Remove(media);
        }

        await _dbContext.SaveChangesAsync();
    }
}