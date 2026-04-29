using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface IAudioMediaScraper
{
    Circle? GetAudioCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetAudioScreenWriters(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetAudioIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetAudioVoiceActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetAudioMusicians(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetAudioAuthors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class AudioMediaScraperExtensions
{
    public static async Task<AudioMedia> GetAudioMediaAsync(this IAudioMediaScraper audioScraper, MediaBase mediaBase,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Audio] 开始解析音频信息");
        }

        var audioMedia = new AudioMedia(mediaBase)
        {
            Circle = audioScraper.GetAudioCircle(htmlDocument, progressReporter),
            ScreenWriters = audioScraper.GetAudioScreenWriters(htmlDocument, progressReporter),
            Illustrators = audioScraper.GetAudioIllustrators(htmlDocument, progressReporter),
            VoiceActors = audioScraper.GetAudioVoiceActors(htmlDocument, progressReporter),
            Musicians = audioScraper.GetAudioMusicians(htmlDocument, progressReporter),
            Authors = audioScraper.GetAudioAuthors(htmlDocument, progressReporter)
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Audio] 音频信息解析完成");
        }

        return audioMedia;
    }
}
