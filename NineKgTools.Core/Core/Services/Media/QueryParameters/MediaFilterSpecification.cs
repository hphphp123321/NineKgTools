using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Services.Media.QueryParameters;

/// <summary>
/// 媒体筛选规范
/// 根据 MediaQueryParameters 应用各种筛选条件
/// </summary>
public class MediaFilterSpecification(MediaQueryParameters parameters) : ISpecification<MediaBase>
{
    public IQueryable<MediaBase> Apply(IQueryable<MediaBase> query)
    {
        // 基础筛选：按名称
        if (!string.IsNullOrWhiteSpace(parameters.Name))
        {
            query = query.Where(m => m.Title.Contains(parameters.Name));
        }

        // 分类筛选逻辑
        if (parameters.FilterByTopCategoryOnly)
        {
            // 仅按顶级分类筛选，不限制具体分类
            if (parameters.TopCategory != null)
            {
                query = query.Where(m => m.Category.TopCategory == parameters.TopCategory);
            }
        }
        else
        {
            // 多分类筛选优先
            if (parameters.Categories is { Count: > 0 })
            {
                var categoryIds = parameters.Categories.Select(c => c.Id).ToList();
                query = query.Where(m => categoryIds.Contains(m.Category.Id));
            }
            // 单分类筛选（向后兼容）
            else if (parameters.Category != null)
            {
                query = query.Where(m => m.Category == parameters.Category);
            }
            // 默认顶级分类筛选
            else if (parameters.TopCategory != null)
            {
                query = query.Where(m => m.Category.TopCategory == parameters.TopCategory);
            }
        }

        // 标签筛选
        if (parameters.TagNames is { Count: > 0 })
        {
            query = query.Where(m => m.Tags.Any(t => parameters.TagNames.Contains(t.Name)));
        }

        // 收藏夹筛选
        if (parameters.FavoriteNames is { Count: > 0 })
        {
            query = query.Where(m => m.Favorites.Any(f => parameters.FavoriteNames.Contains(f.Name)));
        }

        // 社团筛选
        if (parameters.CircleId.HasValue)
        {
            query = query.Where(m => m.Circle != null && m.Circle.Id == parameters.CircleId.Value);
        }

        // 创作者筛选（使用新的统一 Creators 关系）
        if (parameters.CreatorId.HasValue)
        {
            query = query.Where(m => m.Creators.Any(c => c.Id == parameters.CreatorId.Value));
        }

        // 评分范围筛选
        if (parameters.MinRating.HasValue)
        {
            query = query.Where(m => m.Rating >= parameters.MinRating.Value);
        }

        if (parameters.MaxRating.HasValue)
        {
            query = query.Where(m => m.Rating <= parameters.MaxRating.Value);
        }

        // 日期范围筛选
        if (parameters.StartDate.HasValue || parameters.EndDate.HasValue)
        {
            query = parameters.DateType switch
            {
                DateFilterType.ReleaseDate => ApplyReleaseDateFilter(query, parameters.StartDate, parameters.EndDate),
                DateFilterType.StoreDate => ApplyStoreDateFilter(query, parameters.StartDate, parameters.EndDate),
                DateFilterType.LastOpenDate => ApplyLastOpenDateFilter(query, parameters.StartDate, parameters.EndDate),
                _ => ApplyStoreDateFilter(query, parameters.StartDate, parameters.EndDate) // 默认使用入库日期
            };
        }

        return query;
    }

    /// <summary>
    /// 应用发售日期范围筛选
    /// </summary>
    private static IQueryable<MediaBase> ApplyReleaseDateFilter(
        IQueryable<MediaBase> query,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            query = query.Where(m => m.ReleaseDate.HasValue && m.ReleaseDate.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.ReleaseDate.HasValue && m.ReleaseDate.Value <= endDate.Value);
        }

        return query;
    }

    /// <summary>
    /// 应用入库日期范围筛选
    /// </summary>
    private static IQueryable<MediaBase> ApplyStoreDateFilter(
        IQueryable<MediaBase> query,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            query = query.Where(m => m.StoreDate.HasValue && m.StoreDate.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.StoreDate.HasValue && m.StoreDate.Value <= endDate.Value);
        }

        return query;
    }

    /// <summary>
    /// 应用最后打开日期范围筛选
    /// </summary>
    private static IQueryable<MediaBase> ApplyLastOpenDateFilter(
        IQueryable<MediaBase> query,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (startDate.HasValue)
        {
            query = query.Where(m => m.LastOpenDate.HasValue && m.LastOpenDate.Value >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(m => m.LastOpenDate.HasValue && m.LastOpenDate.Value <= endDate.Value);
        }

        return query;
    }
}
