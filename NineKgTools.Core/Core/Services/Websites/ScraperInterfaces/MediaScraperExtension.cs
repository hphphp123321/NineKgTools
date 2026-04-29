using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public static class MediaScraperExtension
{
    public static async Task<MediaBase> ScrapeMediaFromHtmlAsync(this IBaseMediaScraper baseScraper, MediaSource mediaSource,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mediaBase = await baseScraper.GetMediaBaseAsync(mediaSource, htmlDocument, progressReporter, cancellationToken);

        switch (mediaBase.Category.TopCategory)
        {
            case TopCategory.Picture:
                if (baseScraper is IPictureMediaScraper pictureMediaScraper)
                {
                    return await pictureMediaScraper.GetPictureMediaAsync(mediaBase, htmlDocument, progressReporter);
                }
                throw new Exception("Picture scraper接口没有被实现");

            case TopCategory.Video:
                if (baseScraper is IVideoMediaScraper videoScraper)
                {
                    return await videoScraper.GetVideoMediaAsync(mediaBase, htmlDocument, progressReporter);
                }
                throw new Exception("Video scraper接口没有被实现");

            case TopCategory.Audio:
                if (baseScraper is IAudioMediaScraper audioScraper)
                {
                    return await audioScraper.GetAudioMediaAsync(mediaBase, htmlDocument, progressReporter);
                }
                throw new Exception("Audio scraper接口没有被实现");

            case TopCategory.Game:
                if (baseScraper is IGameMediaScraper gameScraper)
                {
                    return await gameScraper.GetGameMediaAsync(mediaBase, htmlDocument, progressReporter);
                }
                throw new Exception("Game scraper接口没有被实现");

            case TopCategory.Text:
                if (baseScraper is ITextMediaScraper textScraper)
                {
                    return await textScraper.GetTextMediaAsync(mediaBase, htmlDocument, progressReporter);
                }
                throw new Exception("Text scraper接口没有被实现");

            default:
                mediaBase.Category = StaticCategories.Unknown;
                return mediaBase;
        }
    }

}
