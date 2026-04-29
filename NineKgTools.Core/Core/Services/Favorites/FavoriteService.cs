using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace NineKgTools.Core.Services.Favorites;

/// <summary>
/// 收藏夹服务类
/// 提供收藏夹的增删改查以及媒体与收藏夹关联管理功能
/// </summary>
public class FavoriteService(MediaDbContext dbContext)
{
    /// <summary>
    /// 将媒体添加到默认收藏夹
    /// </summary>
    /// <param name="mediaId">媒体ID</param>
    public async Task AddMediaToDefaultFavoriteAsync(int mediaId)
    {
        try
        {
            var media = await dbContext.Medias.FindAsync(mediaId);
            if (media == null)
            {
                Log.Warning("媒体 {MediaId} 不存在，无法添加到默认收藏夹", mediaId);
                return;
            }

            // 检查媒体是否已在默认收藏夹中
            if (!media.Favorites.Any(f => f.Id == StaticFavorites.DefaultFavorite.Id))
            {
                media.Favorites.Add(StaticFavorites.DefaultFavorite);
                await dbContext.SaveChangesAsync();
                
                Log.Information("成功添加媒体 {MediaTitle}({MediaId}) 到默认收藏夹", media.Title, mediaId);
            }
            else
            {
                Log.Warning("媒体 {MediaTitle}({MediaId}) 已在默认收藏夹中", media.Title, mediaId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加媒体 {MediaId} 到默认收藏夹时发生错误", mediaId);
            throw;
        }
    }
    
    /// <summary>
    /// 移除媒体的所有收藏夹关联
    /// </summary>
    /// <param name="mediaId">媒体ID</param>
    public async Task RemoveMediaFavoriteAsync(int mediaId)
    {
        try
        {
            var media = await dbContext.Medias.FindAsync(mediaId);
            if (media == null)
            {
                Log.Warning("媒体 {MediaId} 不存在，无法移除收藏夹关联", mediaId);
                return;
            }

            var favoriteCount = media.Favorites.Count;
            media.Favorites.Clear();
            await dbContext.SaveChangesAsync();
            
            Log.Information("成功移除媒体 {MediaTitle}({MediaId}) 的所有收藏夹关联，共移除 {Count} 个", 
                media.Title, mediaId, favoriteCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除媒体 {MediaId} 的收藏夹关联时发生错误", mediaId);
            throw;
        }
    }
    
    /// <summary>
    /// 从指定收藏夹中移除媒体
    /// </summary>
    /// <param name="favoriteId">收藏夹ID</param>
    /// <param name="mediaId">媒体ID</param>
    public async Task RemoveMediaFromFavoriteAsync(int favoriteId, int mediaId)
    {
        try
        {
            var media = await dbContext.Medias.Include(m => m.Favorites).FirstOrDefaultAsync(m => m.Id == mediaId);
            if (media == null)
            {
                Log.Warning("媒体 {MediaId} 不存在，无法从收藏夹移除", mediaId);
                return;
            }

            var favorite = media.Favorites.FirstOrDefault(f => f.Id == favoriteId);
            if (favorite != null)
            {
                media.Favorites.Remove(favorite);
                await dbContext.SaveChangesAsync();
                
                Log.Information("成功从收藏夹 {FavoriteName}({FavoriteId}) 中移除媒体 {MediaTitle}({MediaId})", 
                    favorite.Name, favoriteId, media.Title, mediaId);
            }
            else
            {
                Log.Debug("媒体 {MediaTitle}({MediaId}) 不在收藏夹 {FavoriteId} 中", media.Title, mediaId, favoriteId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "从收藏夹 {FavoriteId} 中移除媒体 {MediaId} 时发生错误", favoriteId, mediaId);
            throw;
        }
    }
    
    /// <summary>
    /// 将媒体添加到指定收藏夹
    /// </summary>
    /// <param name="favoriteId">收藏夹ID</param>
    /// <param name="mediaId">媒体ID</param>
    public async Task AddMediaToFavoriteAsync(int favoriteId, int mediaId)
    {
        try
        {
            var favorite = await dbContext.Favorites.FindAsync(favoriteId);
            if (favorite == null)
            {
                Log.Warning("收藏夹 {FavoriteId} 不存在，无法添加媒体", favoriteId);
                return;
            }

            var media = await dbContext.Medias.Include(m => m.Favorites).FirstOrDefaultAsync(m => m.Id == mediaId);
            if (media == null)
            {
                Log.Warning("媒体 {MediaId} 不存在，无法添加到收藏夹", mediaId);
                return;
            }

            // 检查媒体是否已在该收藏夹中
            if (!media.Favorites.Any(f => f.Id == favoriteId))
            {
                media.Favorites.Add(favorite);
                await dbContext.SaveChangesAsync();
                
                Log.Information("成功将媒体 {MediaTitle}({MediaId}) 添加到收藏夹 {FavoriteName}({FavoriteId})", 
                    media.Title, mediaId, favorite.Name, favoriteId);
            }
            else
            {
                Log.Debug("媒体 {MediaTitle}({MediaId}) 已在收藏夹 {FavoriteName}({FavoriteId}) 中", 
                    media.Title, mediaId, favorite.Name, favoriteId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "将媒体 {MediaId} 添加到收藏夹 {FavoriteId} 时发生错误", mediaId, favoriteId);
            throw;
        }
    }
    
    /// <summary>
    /// 删除收藏夹
    /// </summary>
    /// <param name="favoriteId">收藏夹ID</param>
    public async Task RemoveFavoriteAsync(int favoriteId)
    {
        try
        {
            var favorite = await dbContext.Favorites.FindAsync(favoriteId);
            if (favorite == null)
            {
                Log.Warning("收藏夹 {FavoriteId} 不存在，无法删除", favoriteId);
                return;
            }

            // 检查是否为默认收藏夹
            if (favoriteId == StaticFavorites.DefaultFavorite.Id)
            {
                Log.Warning("尝试删除默认收藏夹，操作被拒绝");
                throw new InvalidOperationException("不能删除默认收藏夹");
            }

            var favoriteName = favorite.Name;
            dbContext.Favorites.Remove(favorite);
            await dbContext.SaveChangesAsync();
            
            Log.Information("成功删除收藏夹 {FavoriteName}({FavoriteId})", favoriteName, favoriteId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除收藏夹 {FavoriteId} 时发生错误", favoriteId);
            throw;
        }
    }
    
    /// <summary>
    /// 添加新收藏夹
    /// </summary>
    /// <param name="favorite">收藏夹对象</param>
    public async Task AddFavoriteAsync(Favorite favorite)
    {
        try
        {
            // 检查收藏夹名称是否重复
            var existingFavorite = await dbContext.Favorites
                .FirstOrDefaultAsync(f => f.Name == favorite.Name);
            if (existingFavorite != null)
            {
                Log.Warning("收藏夹名称 {FavoriteName} 已存在", favorite.Name);
                throw new InvalidOperationException($"收藏夹名称 '{favorite.Name}' 已存在");
            }
            
            await dbContext.Favorites.AddAsync(favorite);
            await dbContext.SaveChangesAsync();
            
            Log.Information("成功添加新收藏夹 {FavoriteName}({FavoriteId})", favorite.Name, favorite.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加收藏夹 {FavoriteName} 时发生错误", favorite.Name);
            throw;
        }
    }
    
    /// <summary>
    /// 更新收藏夹信息
    /// </summary>
    /// <param name="favorite">收藏夹对象</param>
    public async Task UpdateFavoriteAsync(Favorite favorite)
    {
        try
        {
            var dbFavorite = await dbContext.Favorites.FindAsync(favorite.Id);
            if (dbFavorite == null)
            {
                Log.Warning("收藏夹 {FavoriteId} 不存在，无法更新", favorite.Id);
                return;
            }

            // 检查是否为默认收藏夹
            if (favorite.Id == StaticFavorites.DefaultFavorite.Id)
            {
                Log.Warning("尝试更新默认收藏夹，操作被拒绝");
                throw new InvalidOperationException("不能修改默认收藏夹");
            }

            // 检查名称是否重复
            var existingFavorite = await dbContext.Favorites
                .FirstOrDefaultAsync(f => f.Name == favorite.Name && f.Id != favorite.Id);
            if (existingFavorite != null)
            {
                Log.Warning("收藏夹名称 {FavoriteName} 已存在", favorite.Name);
                throw new InvalidOperationException($"收藏夹名称 '{favorite.Name}' 已存在");
            }

            var oldName = dbFavorite.Name;
            dbFavorite.Name = favorite.Name;
            await dbContext.SaveChangesAsync();
            
            Log.Information("成功更新收藏夹 {OldName} -> {NewName}({FavoriteId})", oldName, favorite.Name, favorite.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新收藏夹 {FavoriteId} 时发生错误", favorite.Id);
            throw;
        }
    }
    
    /// <summary>
    /// 获取所有收藏夹列表
    /// </summary>
    /// <returns>收藏夹列表</returns>
    public async Task<List<Favorite>> GetAllFavoritesAsync()
    {
        try
        {
            var favorites = await dbContext.Favorites
                .Include(f => f.Medias)
                .OrderBy(f => f.Id)
                .ToListAsync();
            
            return favorites;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取收藏夹列表时发生错误");
            throw;
        }
    }
    
    /// <summary>
    /// 获取指定收藏夹的媒体信息
    /// </summary>
    /// <param name="favoriteId">收藏夹ID</param>
    /// <returns>收藏夹对象，包含关联的媒体</returns>
    public async Task<Favorite?> GetMediaFavoritesAsync(int favoriteId)
    {
        try
        {
            var favorite = await dbContext.Favorites
                .Include(f => f.Medias)
                    .ThenInclude(m => m.Poster)
                .Include(f => f.Medias)
                    .ThenInclude(m => m.Category)
                .Include(f => f.Medias)
                    .ThenInclude(m => m.Circle)
                .FirstOrDefaultAsync(f => f.Id == favoriteId);
            
            if (favorite == null)
            {
                Log.Warning("收藏夹 {FavoriteId} 不存在", favoriteId);
            }
            
            return favorite;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取收藏夹 {FavoriteId} 媒体信息时发生错误", favoriteId);
            throw;
        }
    }
    
    /// <summary>
    /// 查找指定收藏夹列表中存在的收藏夹
    /// </summary>
    /// <param name="favorites">要查找的收藏夹列表</param>
    /// <returns>存在的收藏夹列表</returns>
    public async Task<List<Favorite>> FindFavoriteAsync(List<Favorite> favorites)
    {
        try
        {
            if (favorites.Count == 0)
            {
                Log.Debug("输入的收藏夹列表为空");
                return new List<Favorite>();
            }
            
            var favoriteIds = favorites.Select(f => f.Id).ToList();
            var result = await dbContext.Favorites
                .Where(f => favoriteIds.Contains(f.Id))
                .ToListAsync();
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "查找收藏夹时发生错误");
            throw;
        }
    }
    
    /// <summary>
    /// 初始化收藏夹数据库，确保默认收藏夹存在
    /// </summary>
    public async Task InitializeFavoritesDb()
    {
        try
        {
            Log.Debug("开始初始化收藏夹数据库");
            
            var dbFavorite = await dbContext.Favorites.AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == StaticFavorites.DefaultFavorite.Id);
                
            if (dbFavorite == null)
            {
                await dbContext.Favorites.AddAsync(StaticFavorites.DefaultFavorite);
                await dbContext.SaveChangesAsync();
                
                Log.Information("成功创建默认收藏夹");
            }
            else
            {
                Log.Debug("默认收藏夹已存在，跳过初始化");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化收藏夹数据库时发生错误");
            throw;
        }
    }
}