using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface IBaseMediaScraper
{
    Task<HtmlDocument?> GetHtmlDocumentAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

    Category GetCategory(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    (string title, List<string> aliasTitles) GetTitle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    DateTime? GetReleaseDate(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    (string summary, string? summaryTranslated) GetSummary(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    (string description, string? descriptionTranslated) GetDescription(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Task<List<Tag>> GetTagsAsync(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

    long GetMediaSize(MediaSource source, HtmlDocument? htmlDocument, IProgressReporter? progressReporter = null);

    List<Uri> GetLinks(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Image? GetPoster(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Image> GetPictures(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    float GetRating(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Dictionary<string, string> Infos(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<string> GetRelatedMediaNames(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class BaseMediaScraperExtensions
{
    public static async Task<MediaBase> GetMediaBaseAsync(this IBaseMediaScraper baseScraper, MediaSource mediaSource, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Base] 开始解析基础信息");
        }

        var mediaBase = new MediaBase
        {
            Source = mediaSource,
            Category = baseScraper.GetCategory(htmlDocument, progressReporter)
        };

        (mediaBase.Title, mediaBase.AliasTitles) = baseScraper.GetTitle(htmlDocument, progressReporter);
        mediaBase.ReleaseDate = baseScraper.GetReleaseDate(htmlDocument, progressReporter);
        mediaBase.StoreDate = DateTime.Now; // 入库日期
        (mediaBase.Summary, mediaBase.SummaryTranslated) = baseScraper.GetSummary(htmlDocument, progressReporter);
        (mediaBase.Description, mediaBase.DescriptionTranslated) = baseScraper.GetDescription(htmlDocument, progressReporter);
        mediaBase.Tags = await baseScraper.GetTagsAsync(htmlDocument, progressReporter, cancellationToken);
        mediaBase.Size = baseScraper.GetMediaSize(mediaSource, htmlDocument, progressReporter);
        mediaBase.Links = baseScraper.GetLinks(htmlDocument, progressReporter);
        mediaBase.Poster = baseScraper.GetPoster(htmlDocument, progressReporter);
        mediaBase.Pictures = baseScraper.GetPictures(htmlDocument, progressReporter);
        mediaBase.Rating = baseScraper.GetRating(htmlDocument, progressReporter);
        mediaBase.Infos = baseScraper.Infos(htmlDocument, progressReporter);

        foreach (var relatedMediaName in baseScraper.GetRelatedMediaNames(htmlDocument, progressReporter))
        {
            // 先只添加标题，后续在MediaService中进行匹配处理
            mediaBase.RelatedMedias.Add(new MediaBase { Title = relatedMediaName });
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Base] 基础信息解析完成: {mediaBase.Title}");
        }

        return mediaBase;
    }
}
