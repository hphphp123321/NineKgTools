using System.Text.Json;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Models.Vectors;
using NineKgTools.Core.Services.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media.Audio;
using NineKgTools.Core.Services.Media.Game;
using NineKgTools.Core.Services.Media.Picture;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Media.Text;
using NineKgTools.Core.Services.Media.Video;
using NineKgTools.Core.Services.Source;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Vectors;
using NineKgTools.Utils;
using Microsoft.EntityFrameworkCore;
using Serilog;
using X.PagedList;
using X.PagedList.Extensions;

namespace NineKgTools.Core.Services.Media;

public class MediaService
{
    private readonly Config _config;
    private readonly MediaDbContext _dbContext;
    private readonly SourceService _sourceService;
    private readonly TagService _tagService;
    private readonly CategoryService _categoryService;
    private readonly FavoriteService _favoriteService;
    private readonly GameMediaService _gameMediaService;
    private readonly AudioMediaService _audioMediaService;
    private readonly VideoMediaService _videoMediaService;
    private readonly PictureMediaService _pictureMediaService;
    private readonly TextMediaService _textMediaService;
    private readonly ImageService _imageService;
    private readonly CreatorService _creatorService;
    private readonly VectorService? _vectorService;
    private readonly VectorEmbeddingService? _embeddingService;

    public MediaService(Config config, MediaDbContext dbContext,
        SourceService sourceService, TagService tagService,
        CategoryService categoryService, FavoriteService favoriteService,
        GameMediaService gameMediaService, AudioMediaService audioMediaService,
        VideoMediaService videoMediaService, PictureMediaService pictureMediaService,
        TextMediaService textMediaService, ImageService imageService, CreatorService creatorService,
        VectorService? vectorService = null, VectorEmbeddingService? embeddingService = null)
    {
        _config = config;
        _dbContext = dbContext;
        _sourceService = sourceService;
        _tagService = tagService;
        _categoryService = categoryService;
        _favoriteService = favoriteService;
        _gameMediaService = gameMediaService;
        _audioMediaService = audioMediaService;
        _videoMediaService = videoMediaService;
        _pictureMediaService = pictureMediaService;
        _textMediaService = textMediaService;
        _imageService = imageService;
        _creatorService = creatorService;
        _vectorService = vectorService;
        _embeddingService = embeddingService;
    }

    public async Task<MediaBase?> GetMediaAsync(int id)
    {
        var media = await _dbContext.Medias
            .Include(m => m.Circle)
            .Include(m => m.Tags)
            .Include(m => m.Category)
            .Include(m => m.Favorites)
            .Include(m => m.Source)
            .Include(m => m.Poster)
            .Include(m => m.Pictures)
            .Include(m => m.RelatedMedias)
            .Include(m => m.Creators)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (media == null) return media;
        // 根据媒体类型加载具体子类的特定数据
        switch (media)
        {
            case VideoMedia videoMedia:
                await LoadVideoMediaDetails(videoMedia);
                break;
            case AudioMedia audioMedia:
                await LoadAudioMediaDetails(audioMedia);
                break;
            case GameMedia gameMedia:
                await LoadGameMediaDetails(gameMedia);
                break;
            case PictureMedia pictureMedia:
                await LoadPictureMediaDetails(pictureMedia);
                break;
            case TextMedia textMedia:
                await LoadTextMediaDetails(textMedia);
                break;
        }

        return media;
    }

    /// <summary>
    /// 获取简化的媒体信息，仅包含SimpleMediaCard所需的属性
    /// </summary>
    /// <param name="id">媒体ID</param>
    /// <returns>包含基本信息的媒体对象</returns>
    public async Task<MediaBase?> GetSimpleMediaAsync(int id)
    {
        var media = await _dbContext.Medias
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.Poster)
            .FirstOrDefaultAsync(m => m.Id == id);

        return media;
    }

    /// <summary>
    /// 获取MediaCard所需的媒体信息（包含Circle、Category、Poster、Rating）
    /// </summary>
    /// <param name="id">媒体ID</param>
    /// <returns>包含卡片显示所需信息的媒体对象</returns>
    public async Task<MediaBase?> GetMediaForCardAsync(int id)
    {
        var media = await _dbContext.Medias
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.Poster)
            .Include(m => m.Circle)
            .FirstOrDefaultAsync(m => m.Id == id);

        return media;
    }

    /// <summary>
    /// 获取分页的 Media 列表，支持多种过滤和排序方式
    /// </summary>
    /// <param name="parameters">查询参数</param>
    /// <returns>分页的 Media 列表</returns>
    public IPagedList<MediaBase> GetPagedMediaList(MediaQueryParameters parameters)
    {
        // 构建基础查询
        var query = _dbContext.Medias
            .AsNoTracking()
            .Include(m => m.Circle)
            .Include(m => m.Tags)
            .Include(m => m.Category)
            .Include(m => m.Favorites)
            .Include(m => m.Source)
            .Include(m => m.Poster)
            .Include(m => m.Pictures)
            .Include(m => m.RelatedMedias)
            .AsQueryable();

        // 应用过滤规范
        var filterSpec = new MediaFilterSpecification(parameters);
        query = filterSpec.Apply(query);

        // 应用排序规范
        var sortSpec = new MediaSortSpecification(parameters.SortOption);
        query = sortSpec.Apply(query);

        // 使用 X.PagedList 进行分页
        return query.ToPagedList(parameters.PageNumber, parameters.PageSize);
    }

    /// <summary>
    /// 批量添加或更新媒体信息
    /// </summary>
    /// <param name="medias"></param>
    /// <returns></returns>
    public async Task<IEnumerable<MediaBase?>> AddOrUpdateMediaAsync(IEnumerable<MediaBase> medias)
    {
        return await Task.WhenAll(medias.Select(AddOrUpdateMediaAsync));
    }

    /// <summary>
    /// 添加或更新媒体信息
    /// </summary>
    /// <param name="media"></param>
    /// <returns></returns>
    public async Task<MediaBase?> AddOrUpdateMediaAsync(MediaBase media)
    {
        // 如果对应 MediaSource 已有关联 Media（重新识别 / 手动重入库场景），
        // 先按路径删除旧记录再插入新记录——从用户视角这是"用新识别结果替换旧记录"的预期行为。
        // 注意：删除旧 Media 只清理向量/图片/外键，MediaSource 本身保留，因此新 Media 能关联同一 Source。
        if (media.Source is { InDatabase: true })
        {
            var exist = await MediaExistAsync(media.Source);
            if (exist)
            {
                Log.Information("媒体源：{MediaSourceFullPath} 已有关联 Media，将删除旧记录后用新结果替换", media.Source.FullPath);
                await RemoveMediaAsync(media.Source);
            }
        }

        await UpdateMediaBaseInfoAsync(media);

        var result = media.Category.TopCategory switch
        {
            TopCategory.Picture => await _pictureMediaService.AddOrUpdatePictureAsync((PictureMedia)media),
            TopCategory.Video => await _videoMediaService.AddOrUpdateVideoAsync((VideoMedia)media),
            TopCategory.Audio => await _audioMediaService.AddOrUpdateAudioAsync((AudioMedia)media),
            TopCategory.Game => await _gameMediaService.AddOrUpdateGameAsync((GameMedia)media),
            TopCategory.Text => await _textMediaService.AddOrUpdateTextAsync((TextMedia)media),
            _ => media
        };
        
        // 添加或更新向量数据库
        if (result != null)
        {
            await StoreOrUpdateMediaVectorAsync(result);
        }
        
        return result;
    }

    /// <summary>
    /// 判断媒体是否存在
    /// </summary>
    /// <param name="mediaSource">媒体源</param>
    /// <returns></returns>
    public async Task<bool> MediaExistAsync(MediaSource mediaSource)
    {
        return await _dbContext.Medias.AnyAsync(m =>
            m.Source != null && m.Source.FullPath == mediaSource.FullPath);
    }

    /// <summary>
    /// 按媒体源路径查找关联 Media 的 Id（若存在）。用于"手动添加"流程的重复检测：
    /// 当用户选中的路径已有入库媒体时，直接导航到对应详情页而不是重复建档。
    /// </summary>
    /// <param name="fullPath">媒体源完整路径</param>
    /// <returns>关联 Media 的 Id；若路径未关联任何 Media 则返回 null</returns>
    public async Task<int?> GetMediaIdByFullPathAsync(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return null;

        return await _dbContext.Medias
            .AsNoTracking()
            .Where(m => m.Source != null && m.Source.FullPath == fullPath)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync();
    }

    public async Task RemoveMediaAsync(MediaSource mediaSource)
    {
        var media = await _dbContext.Medias
            .Include(m => m.Source)
            .FirstOrDefaultAsync(m =>
                m.Source != null && m.Source.FullPath == mediaSource.FullPath);

        if (media == null) return;

        await RemoveMediaAsync(media);
        Log.Information("删除媒体：{Title} 完成", media.Title);
    }

    private async Task RemoveMediaAsync(MediaBase media)
    {
        // 删除向量数据库中的记录
        await DeleteMediaVectorAsync(media.Id);
        
        // 断开媒体和图片之间的循环引用
        if (media.Poster != null)
        {
            var poster = media.Poster;
            media.Poster = null; // 先解除媒体到海报的引用
            await _dbContext.SaveChangesAsync(); // 保存这个变更

            await _imageService.RemoveImageAsync(poster); // 删除海报
        }

        // 清理其他媒体图片引用
        if (media.Pictures.Count != 0)
        {
            var pictures = media.Pictures.ToList();
            media.Pictures.Clear();

            await _dbContext.SaveChangesAsync(); // 保存这个变更

            await _imageService.RemoveImagesAsync(pictures); // 删除图片
        }

        _dbContext.Medias.Remove(media);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 删除媒体
    /// </summary>
    /// <param name="id">媒体ID</param>
    /// <returns></returns>
    public async Task RemoveMediaAsync(int id)
    {
        var media = await _dbContext.Medias.FirstOrDefaultAsync(m => m.Id == id);
        if (media == null) return;
        await RemoveMediaAsync(media);
    }

    /// <summary>
    /// 寻找一组Media
    /// </summary>
    public async Task<List<MediaBase?>> FindMediasAsync(List<MediaBase> medias)
    {
        return [..await Task.WhenAll(medias.Select(m => FindMediaAsync(m.Title)))];
    }

    /// <summary>
    /// 通过标题寻找Media
    /// </summary>
    public async Task<MediaBase?> FindMediaAsync(string title)
    {
        // 进行模糊匹配 /statuslineTODO 模糊匹配更加精确智能一些
        return await _dbContext.Medias.FirstOrDefaultAsync(m => m.Title.Contains(title));
    }

    /// <summary>
    /// 获取所有媒体记录
    /// </summary>
    public async Task<List<MediaBase>> GetAllMedia()
    {
        return await _dbContext.Medias
            .Include(m => m.Source)
            .ToListAsync();
    }

    /// <summary>
    /// 更新媒体基本信息，把媒体中一些在数据库中有可能存在的字段更新到媒体中
    /// </summary>
    /// <param name="media"></param>
    private async Task UpdateMediaBaseInfoAsync(MediaBase media)
    {
        media.Circle = media.Circle == null ? null : await _creatorService.AddOrUpdateCircle(media.Circle);
        media.Source ??= await _sourceService.FindMediaSourceAsync(media.Source);
        media.Category = await _categoryService.FindCategoryAsync(media.Category);
        media.Favorites = await _favoriteService.FindFavoriteAsync(media.Favorites);
        media.Tags = await _tagService.FindTagsAsync(media.Tags);
        media.Poster = await _imageService.AddOrFindImageAsync(media.Poster, media.Title);
        media.Pictures = await _imageService.AddOrFindImagesAsync(media.Pictures, media.Title);

        // 更新相关媒体信息
        var medias = await FindMediasAsync(media.RelatedMedias);
        media.RelatedMedias = medias.Where(m => m != null).Select(m => m!).ToList();
    }

    // 加载视频媒体特定信息
    private async Task LoadVideoMediaDetails(VideoMedia videoMedia)
    {
        // 加载视频特有的关联信息
        await _dbContext.Entry(videoMedia)
            .Collection(v => v.Directors)
            .LoadAsync();

        await _dbContext.Entry(videoMedia)
            .Collection(v => v.ScreenWriters)
            .LoadAsync();

        await _dbContext.Entry(videoMedia)
            .Collection(v => v.Illustrators)
            .LoadAsync();

        await _dbContext.Entry(videoMedia)
            .Collection(v => v.Actors)
            .LoadAsync();

        await _dbContext.Entry(videoMedia)
            .Collection(v => v.Musicians)
            .LoadAsync();

        await _dbContext.Entry(videoMedia)
            .Collection(v => v.Makers)
            .LoadAsync();
    }

    // 加载音频媒体特定信息
    private async Task LoadAudioMediaDetails(AudioMedia audioMedia)
    {
        await _dbContext.Entry(audioMedia)
            .Collection(a => a.VoiceActors)
            .LoadAsync();

        await _dbContext.Entry(audioMedia)
            .Collection(a => a.ScreenWriters)
            .LoadAsync();

        await _dbContext.Entry(audioMedia)
            .Collection(a => a.Illustrators)
            .LoadAsync();

        await _dbContext.Entry(audioMedia)
            .Collection(a => a.Musicians)
            .LoadAsync();

        await _dbContext.Entry(audioMedia)
            .Collection(a => a.Authors)
            .LoadAsync();
    }

    // 加载游戏媒体特定信息
    private async Task LoadGameMediaDetails(GameMedia gameMedia)
    {
        await _dbContext.Entry(gameMedia)
            .Collection(g => g.ScreenWriters)
            .LoadAsync();

        await _dbContext.Entry(gameMedia)
            .Collection(g => g.Illustrators)
            .LoadAsync();

        await _dbContext.Entry(gameMedia)
            .Collection(g => g.VoiceActors)
            .LoadAsync();

        await _dbContext.Entry(gameMedia)
            .Collection(g => g.Musicians)
            .LoadAsync();

        await _dbContext.Entry(gameMedia)
            .Collection(g => g.Authors)
            .LoadAsync();
    }

    // 加载图片媒体特定信息
    private async Task LoadPictureMediaDetails(PictureMedia pictureMedia)
    {
        await _dbContext.Entry(pictureMedia)
            .Collection(p => p.Illustrators)
            .LoadAsync();

        await _dbContext.Entry(pictureMedia)
            .Collection(p => p.Actors)
            .LoadAsync();

        await _dbContext.Entry(pictureMedia)
            .Collection(p => p.Authors)
            .LoadAsync();
    }

    // 加载文本媒体特定信息
    private async Task LoadTextMediaDetails(TextMedia textMedia)
    {
        await _dbContext.Entry(textMedia)
            .Collection(t => t.Illustrators)
            .LoadAsync();

        await _dbContext.Entry(textMedia)
            .Reference(t => t.Author)
            .LoadAsync();
    }

    /// <summary>
    /// 更新现有媒体
    /// </summary>
    /// <param name="media">要更新的媒体</param>
    /// <returns>更新后的媒体</returns>
    public async Task<MediaBase?> UpdateMediaAsync(MediaBase media)
    {   
        try
        {
            // 检查媒体是否存在
            var existingMedia = await _dbContext.Medias
                .AsNoTracking()
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == media.Id);
            
            if (existingMedia == null)
            {
                Log.Warning("尝试更新不存在的媒体，ID: {MediaId}", media.Id);
                return null;
            }
            
            // 检查顶层分类是否改变
            var topCategoryChanged = existingMedia.Category.TopCategory != media.Category.TopCategory;
            
            if (topCategoryChanged)
            {
                var newMedia = media.Copy();
                newMedia = media.Category.TopCategory switch
                {
                    TopCategory.Picture => new PictureMedia(newMedia),
                    TopCategory.Video => new VideoMedia(newMedia),
                    TopCategory.Audio => new AudioMedia(newMedia),
                    TopCategory.Game => new GameMedia(newMedia),
                    TopCategory.Text => new TextMedia(newMedia),
                    _ => newMedia
                };

                // 如果顶层分类改变，先删除旧媒体
                await RemoveMediaAsync(media);
                await AddOrUpdateMediaAsync(newMedia);
                Log.Information("媒体{MediaTitle}的顶层类型已更改为{NewCategory}，旧媒体条目已删除", media.Title, media.Category.TopCategory);
            }
            
            // 更新媒体
            await _dbContext.SaveChangesAsync();
            
            // 更新向量数据库
            await StoreOrUpdateMediaVectorAsync(media);
            
            Log.Information("媒体{MediaTitle}更新完成", media.Title);
            return media;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新媒体失败: {MediaTitle}", media.Title);
            return null;
        }

    }

    #region 相关媒体管理

    /// <summary>
    /// 简单搜索媒体（标题包含查询）
    /// </summary>
    /// <param name="searchTerm">搜索关键词</param>
    /// <param name="maxResults">最大结果数</param>
    /// <param name="excludeMediaId">排除的媒体ID（用于排除当前媒体）</param>
    /// <returns>匹配的媒体列表</returns>
    public async Task<List<MediaBase>> SearchMediaByTitleAsync(
        string searchTerm,
        int maxResults = 20,
        int? excludeMediaId = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<MediaBase>();

        var query = _dbContext.Medias
            .Include(m => m.Poster)
            .Include(m => m.Category)
            .AsNoTracking()
            .Where(m => m.Title.Contains(searchTerm));

        if (excludeMediaId.HasValue)
            query = query.Where(m => m.Id != excludeMediaId.Value);

        return await query
            .Take(maxResults)
            .ToListAsync();
    }

    /// <summary>
    /// 添加双向相关媒体关联
    /// </summary>
    /// <param name="mediaId">当前媒体ID</param>
    /// <param name="relatedMediaId">要关联的媒体ID</param>
    public async Task AddRelatedMediaAsync(int mediaId, int relatedMediaId)
    {
        if (mediaId == relatedMediaId)
        {
            Log.Warning("不能将媒体与自己关联");
            return;
        }

        var media = await _dbContext.Medias
            .Include(m => m.RelatedMedias)
            .FirstOrDefaultAsync(m => m.Id == mediaId);
        var relatedMedia = await _dbContext.Medias
            .Include(m => m.RelatedMedias)
            .FirstOrDefaultAsync(m => m.Id == relatedMediaId);

        if (media == null || relatedMedia == null)
        {
            Log.Warning("添加相关媒体失败：媒体不存在 (mediaId: {MediaId}, relatedMediaId: {RelatedMediaId})",
                mediaId, relatedMediaId);
            return;
        }

        // 双向添加
        if (!media.RelatedMedias.Any(m => m.Id == relatedMediaId))
            media.RelatedMedias.Add(relatedMedia);
        if (!relatedMedia.RelatedMedias.Any(m => m.Id == mediaId))
            relatedMedia.RelatedMedias.Add(media);

        await _dbContext.SaveChangesAsync();
        Log.Information("已添加双向相关媒体关联: {MediaTitle} <-> {RelatedMediaTitle}",
            media.Title, relatedMedia.Title);
    }

    /// <summary>
    /// 移除双向相关媒体关联
    /// </summary>
    /// <param name="mediaId">当前媒体ID</param>
    /// <param name="relatedMediaId">要移除关联的媒体ID</param>
    public async Task RemoveRelatedMediaAsync(int mediaId, int relatedMediaId)
    {
        var media = await _dbContext.Medias
            .Include(m => m.RelatedMedias)
            .FirstOrDefaultAsync(m => m.Id == mediaId);
        var relatedMedia = await _dbContext.Medias
            .Include(m => m.RelatedMedias)
            .FirstOrDefaultAsync(m => m.Id == relatedMediaId);

        if (media == null || relatedMedia == null)
        {
            Log.Warning("移除相关媒体失败：媒体不存在 (mediaId: {MediaId}, relatedMediaId: {RelatedMediaId})",
                mediaId, relatedMediaId);
            return;
        }

        // 双向移除
        var mediaToRemove = media.RelatedMedias.FirstOrDefault(m => m.Id == relatedMediaId);
        if (mediaToRemove != null)
            media.RelatedMedias.Remove(mediaToRemove);

        var relatedToRemove = relatedMedia.RelatedMedias.FirstOrDefault(m => m.Id == mediaId);
        if (relatedToRemove != null)
            relatedMedia.RelatedMedias.Remove(relatedToRemove);

        await _dbContext.SaveChangesAsync();
        Log.Information("已移除双向相关媒体关联: {MediaTitle} <-> {RelatedMediaTitle}",
            media.Title, relatedMedia.Title);
    }

    #endregion
    
    #region 向量数据库相关方法
    
    /// <summary>
    /// 判断是否应该使用向量存储
    /// </summary>
    private bool ShouldUseVectorStorage()
    {
        return _config?.Ai?.UseAi == true &&
               _config?.Ai?.Vector?.Enable == true &&
               _config?.Ai?.Vector?.Media?.Enable == true &&
               _vectorService != null &&
               _embeddingService != null;
    }
    
    /// <summary>
    /// 存储或更新媒体向量
    /// </summary>
    private async Task StoreOrUpdateMediaVectorAsync(MediaBase media, CancellationToken cancellationToken = default)
    {
        if (!ShouldUseVectorStorage())
            return;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vectorId = $"media_{media.Id}";

            // 使用公共方法创建媒体向量
            var mediaVector = await CreateMediaVectorAsync(media, cancellationToken);

            // 检查是否已存在
            if (await _vectorService!.ExistsMediaAsync(vectorId, cancellationToken))
            {
                await _vectorService.UpdateMediaVectorAsync(mediaVector, cancellationToken);
                Log.Debug("更新媒体向量: {Id} - {Title}", media.Id, media.Title);
            }
            else
            {
                await _vectorService.AddMediaVectorAsync(mediaVector, cancellationToken);
                Log.Debug("添加媒体向量: {Id} - {Title}", media.Id, media.Title);
            }
        }
        catch (OperationCanceledException)
        {
            Log.Information("存储媒体向量操作已被取消: {MediaId} - {MediaTitle}", media.Id, media.Title);
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                // 向量操作失败不应影响主流程
                Log.Warning(ex, "存储媒体向量失败: {MediaId} - {MediaTitle}", media.Id, media.Title);
            }
        }
    }
    
    /// <summary>
    /// 删除媒体向量
    /// </summary>
    private async Task DeleteMediaVectorAsync(int mediaId)
    {
        if (!ShouldUseVectorStorage())
            return;
        
        try
        {
            var vectorId = $"media_{mediaId}";
            await _vectorService!.DeleteMediaVectorAsync(vectorId);
            Log.Debug("删除媒体向量: {Id}", mediaId);
        }
        catch (Exception ex)
        {
            // 向量操作失败不应影响主流程
            Log.Warning(ex, "删除媒体向量失败: {MediaId}", mediaId);
        }
    }
    
    /// <summary>
    /// 创建媒体向量对象（包含嵌入向量）
    /// </summary>
    public async Task<MediaVector> CreateMediaVectorAsync(MediaBase media, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var text = BuildMediaTextForVector(media);
        var embedding = await _embeddingService!.GenerateEmbeddingAsync(text, cancellationToken);

        return new MediaVector
        {
            Id = $"media_{media.Id}",
            MediaId = media.Id,
            MediaTitle = media.Title,
            Text = text,
            Embedding = embedding,
            MediaType = GetMediaType(media),
            CategoryName = media.Category?.Name,
            CategoryId = media.Category?.Id,
            CircleName = media.Circle?.Name,
            CircleId = media.Circle?.Id,
            Summary = media.Summary,
            ReleaseDateString = media.ReleaseDate?.ToString("yyyy-MM-dd"),
            Rating = media.Rating,
            TagsJson = JsonSerializer.Serialize(media.Tags.Select(t => t.Name).ToList()),
            AliasesJson = JsonSerializer.Serialize(media.AliasTitles)
        };
    }
    
    /// <summary>
    /// 构建媒体文本用于向量化
    /// </summary>
    public string BuildMediaTextForVector(MediaBase media)
    {
        var parts = new List<string> { media.Title };
        
        // 添加别名
        if (media.AliasTitles.Any())
        {
            parts.AddRange(media.AliasTitles);
        }
        
        // 添加简介
        if (!string.IsNullOrWhiteSpace(media.Summary))
        {
            parts.Add(media.Summary);
        }
        
        
        // 添加分类
        if (media.Category != null)
        {
            parts.Add($"分类:{media.Category.Name}");
        }
        
        // 添加社团
        if (media.Circle != null)
        {
            parts.Add($"社团:{media.Circle.Name}");
        }
        
        // 添加标签
        if (media.Tags.Any())
        {
            parts.Add($"标签:{string.Join(",", media.Tags.Select(t => t.Name))}");
        }
        
        return string.Join(" ", parts);
    }
    
    /// <summary>
    /// 获取媒体类型
    /// </summary>
    public string GetMediaType(MediaBase media)
    {
        return media.GetType().Name.Replace("Media", "");
    }
    
    #endregion
}