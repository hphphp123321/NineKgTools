using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface IPictureMediaScraper
{
    int GetPicturePageNum(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Circle? GetPictureCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetPictureIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetPictureActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetPictureAuthors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class ImageMediaScraperExtensions
{
    public static async Task<PictureMedia> GetPictureMediaAsync(this IPictureMediaScraper pictureScraper, MediaBase mediaBase,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Picture] 开始解析图片信息");
        }

        var pictureMedia = new PictureMedia(mediaBase)
        {
            PageNum = pictureScraper.GetPicturePageNum(mediaBase.Source, htmlDocument, progressReporter),
            Circle = pictureScraper.GetPictureCircle(htmlDocument, progressReporter),
            Illustrators = pictureScraper.GetPictureIllustrators(htmlDocument, progressReporter),
            Actors = pictureScraper.GetPictureActors(htmlDocument, progressReporter),
            Authors = pictureScraper.GetPictureAuthors(htmlDocument, progressReporter)
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Picture] 图片信息解析完成");
        }

        return pictureMedia;
    }
}
