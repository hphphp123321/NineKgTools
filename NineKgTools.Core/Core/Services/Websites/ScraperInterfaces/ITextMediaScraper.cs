using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface ITextMediaScraper
{
    int GetTextWordCount(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    int GetTextBookNum(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Circle? GetTextCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetTextIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Creator? GetTextAuthor(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class TextMediaScraperExtensions
{
    public static async Task<TextMedia> GetTextMediaAsync(this ITextMediaScraper textScraper, MediaBase mediaBase,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Text] 开始解析文本信息");
        }

        var textMedia = new TextMedia(mediaBase)
        {
            WordCount = textScraper.GetTextWordCount(mediaBase.Source, htmlDocument, progressReporter),
            BookNum = textScraper.GetTextBookNum(mediaBase.Source, htmlDocument, progressReporter),
            Circle = textScraper.GetTextCircle(htmlDocument, progressReporter),
            Illustrators = textScraper.GetTextIllustrators(htmlDocument, progressReporter),
            Author = textScraper.GetTextAuthor(htmlDocument, progressReporter)
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Text] 文本信息解析完成");
        }

        return textMedia;
    }
}
