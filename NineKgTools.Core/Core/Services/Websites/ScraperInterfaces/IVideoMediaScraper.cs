using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface IVideoMediaScraper
{
    int GetVideoEpisodes(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Circle? GetVideoCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Circle>? GetVideoMakers(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetVideoScreenWriters(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetVideoIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetVideoActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetVideoMusicians(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetVideoDirectors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class VideoMediaScraperExtensions
{
    public static async Task<VideoMedia> GetVideoMediaAsync(this IVideoMediaScraper videoScraper, MediaBase mediaBase,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Video] 开始解析视频信息");
        }

        var videoMedia = new VideoMedia(mediaBase)
        {
            Episodes = videoScraper.GetVideoEpisodes(mediaBase.Source, htmlDocument, progressReporter),
            Circle = videoScraper.GetVideoCircle(htmlDocument, progressReporter),
            Makers = videoScraper.GetVideoMakers(htmlDocument, progressReporter),
            ScreenWriters = videoScraper.GetVideoScreenWriters(htmlDocument, progressReporter),
            Illustrators = videoScraper.GetVideoIllustrators(htmlDocument, progressReporter),
            Actors = videoScraper.GetVideoActors(htmlDocument, progressReporter),
            Musicians = videoScraper.GetVideoMusicians(htmlDocument, progressReporter),
            Directors = videoScraper.GetVideoDirectors(htmlDocument, progressReporter)
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Video] 视频信息解析完成");
        }

        return videoMedia;
    }
}
