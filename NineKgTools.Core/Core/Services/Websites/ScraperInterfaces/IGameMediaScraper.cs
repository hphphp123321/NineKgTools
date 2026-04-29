using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Services.Tasks.Interfaces;
using HtmlAgilityPack;

namespace NineKgTools.Core.Services.Websites.ScraperInterfaces;

public interface IGameMediaScraper
{
    List<Platform> GetGameSupportedPlatforms(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    Circle? GetGameCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetGameScreenWriters(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetGameIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetGameVoiceActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetGameMusicians(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);

    List<Creator>? GetGameAuthors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null);
}

public static class GameMediaScraperExtensions
{
    public static async Task<GameMedia> GetGameMediaAsync(this IGameMediaScraper gameScraper, MediaBase mediaBase,
        HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Game] 开始解析游戏信息");
        }

        var gameMedia = new GameMedia(mediaBase)
        {
            Platforms = gameScraper.GetGameSupportedPlatforms(htmlDocument, progressReporter),
            Circle = gameScraper.GetGameCircle(htmlDocument, progressReporter),
            ScreenWriters = gameScraper.GetGameScreenWriters(htmlDocument, progressReporter),
            Illustrators = gameScraper.GetGameIllustrators(htmlDocument, progressReporter),
            VoiceActors = gameScraper.GetGameVoiceActors(htmlDocument, progressReporter),
            Musicians = gameScraper.GetGameMusicians(htmlDocument, progressReporter),
            Authors = gameScraper.GetGameAuthors(htmlDocument, progressReporter)
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Game] 游戏信息解析完成");
        }

        return gameMedia;
    }
}
