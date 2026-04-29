using NineKgTools.Core.Models.Categories;

namespace NineKgTools.Core.Services.Media.QueryParameters;

/// <summary>
/// 日期筛选类型枚举
/// </summary>
public enum DateFilterType
{
    /// <summary>
    /// 发售日期
    /// </summary>
    ReleaseDate,

    /// <summary>
    /// 入库日期
    /// </summary>
    StoreDate,

    /// <summary>
    /// 最后打开日期
    /// </summary>
    LastOpenDate
}

public class MediaQueryParameters
{
    private const int MaxPageSize = 100;
    private int _pageSize = 10;

    // 基础筛选
    public string? Name { get; set; }
    public TopCategory? TopCategory { get; set; }

    /// <summary>
    /// 单个分类筛选（兼容旧代码）
    /// </summary>
    public Category? Category { get; set; }

    /// <summary>
    /// 多分类筛选
    /// </summary>
    public List<Category>? Categories { get; set; }

    /// <summary>
    /// 是否仅按顶级分类筛选（不限制具体分类）
    /// </summary>
    public bool FilterByTopCategoryOnly { get; set; }

    // 标签筛选
    public List<string>? TagNames { get; set; }

    // 收藏夹筛选
    public List<string>? FavoriteNames { get; set; }

    // 社团筛选
    public int? CircleId { get; set; }

    // 创作者筛选
    public int? CreatorId { get; set; }

    // 评分范围筛选
    public float? MinRating { get; set; }
    public float? MaxRating { get; set; }

    // 日期范围筛选
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateFilterType? DateType { get; set; }

    // 排序选项
    public MediaSortOption SortOption { get; set; } = MediaSortOption.IdAsc;

    // 分页参数
    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = (value > MaxPageSize) ? MaxPageSize : value;
    }

    public static MediaQueryParameters Default => new();
}
