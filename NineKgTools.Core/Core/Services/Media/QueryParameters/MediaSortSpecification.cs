using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Services.Media.QueryParameters;

public enum MediaSortOption
{
    IdAsc,
    IdDesc,
    TitleAsc,
    TitleDesc,
    SizeAsc,
    SizeDesc,
    ReleaseDateAsc,
    ReleaseDateDesc,
    StoreDateAsc,
    StoreDateDesc,
    LastOpenDateAsc,
    LastOpenDateDesc,
    RatingAsc,
    RatingDesc
}

public class MediaSortSpecification(MediaSortOption sortOption) : ISpecification<MediaBase>
{
    public IQueryable<MediaBase> Apply(IQueryable<MediaBase> query)
    {
        query = sortOption switch
        {
            MediaSortOption.IdAsc => query.OrderBy(m => m.Id),
            MediaSortOption.IdDesc => query.OrderByDescending(m => m.Id),
            MediaSortOption.TitleAsc => query.OrderBy(m => m.Title),
            MediaSortOption.TitleDesc => query.OrderByDescending(m => m.Title),
            MediaSortOption.SizeAsc => query.OrderBy(m => m.Size),
            MediaSortOption.SizeDesc => query.OrderByDescending(m => m.Size),
            MediaSortOption.ReleaseDateAsc => query.OrderBy(m => m.ReleaseDate),
            MediaSortOption.ReleaseDateDesc => query.OrderByDescending(m => m.ReleaseDate),
            MediaSortOption.StoreDateAsc => query.OrderBy(m => m.StoreDate),
            MediaSortOption.StoreDateDesc => query.OrderByDescending(m => m.StoreDate),
            MediaSortOption.LastOpenDateAsc => query.OrderBy(m => m.LastOpenDate),
            MediaSortOption.LastOpenDateDesc => query.OrderByDescending(m => m.LastOpenDate),
            MediaSortOption.RatingAsc => query.OrderBy(m => m.Rating),
            MediaSortOption.RatingDesc => query.OrderByDescending(m => m.Rating),
            _ => query.OrderBy(m => m.Title)
        };

        return query;
    }
}