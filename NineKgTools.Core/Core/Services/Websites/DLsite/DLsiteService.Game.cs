using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.ScraperInterfaces;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

public partial class DLsiteService : IGameMediaScraper
{
    public List<Platform> GetGameSupportedPlatforms(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var osSupport = new List<Platform>();

        var platformsNodes = htmlDocument.DocumentNode
            .SelectNodes("//div[@class='os_popup_body']//table//tr");
        if (platformsNodes == null) goto PlatformNotFound;

        foreach (var platformsNode in platformsNodes)
        {
            var cells = platformsNode.SelectNodes("td");
            if (cells == null || cells.Count != 2) continue;
            string osName = cells[0].InnerText.Trim();
            bool isSupported = cells[1].InnerText.Trim() != "-";
            if (!isSupported) continue;
            switch (osName)
            {
                case "Windows":
                    osSupport.Add(Platform.Windows);
                    break;
                case "Android":
                    osSupport.Add(Platform.Android);
                    break;
                case "iOS":
                    osSupport.Add(Platform.iOS);
                    break;
                case "Mac":
                    osSupport.Add(Platform.Mac);
                    break;
                case "Linux":
                    osSupport.Add(Platform.Linux);
                    break;
                default:
                    osSupport.Add(Platform.Other);
                    break;
            }
        }
        Log.Debug("平台支持: {OsSupport}", osSupport);
        progressReporter?.DebugAsync($"[DLsite/Game] 平台支持: {string.Join(", ", osSupport)}");
        return osSupport;

        PlatformNotFound:
        Log.Debug("平台未知");
        progressReporter?.DebugAsync("[DLsite/Game] 平台: 未知");
        return osSupport;
    }

    public Circle? GetGameCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var circleNode = htmlDocument.DocumentNode.
            SelectSingleNode("//span[@class='maker_name']");
        if (circleNode == null) goto CircleNotFound;

        var circleName = circleNode.InnerText.Trim();
        Log.Debug("制作组: {CircleName}", circleName);
        progressReporter?.DebugAsync($"[DLsite/Game] 制作组: {circleName}");
        return new Circle { Name = circleName };

        CircleNotFound:
        Log.Debug("制作组未知");
        progressReporter?.DebugAsync("[DLsite/Game] 制作组: 未知");
        return null;
    }

    public List<Creator>? GetGameScreenWriters(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var screenWriters = new List<Creator>();
        var screenWritersNode = htmlDocument.DocumentNode
            .SelectSingleNode("//table[@id='work_outline']//tr[th[text()='剧情']]/td");
        if (screenWritersNode == null) goto ScreenWritersNotFound;

        var screenWritersNodes = screenWritersNode.SelectNodes(".//a");
        if (screenWritersNodes == null) goto ScreenWritersNotFound;

        foreach (var screenWriterNode in screenWritersNodes)
        {
            var screenWriter = screenWriterNode.InnerText.Trim();
            screenWriters.Add(new Creator { Name = screenWriter, Types = [CreatorType.ScreenWriter] });
        }
        Log.Debug("编剧: {ScreenWriters}", screenWriters.Select(sw => sw.Name));
        progressReporter?.DebugAsync($"[DLsite/Game] 编剧: {string.Join(", ", screenWriters.Select(sw => sw.Name))}");
        return screenWriters;

        ScreenWritersNotFound:
        Log.Debug("编剧未知");
        progressReporter?.DebugAsync("[DLsite/Game] 编剧: 未知");
        return null;
    }

    public List<Creator>? GetGameIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var illustrators = new List<Creator>();
        var illustratorsNode = htmlDocument.DocumentNode
            .SelectSingleNode("//table[@id='work_outline']//tr[th[text()='插画']]/td");
        if (illustratorsNode == null) goto IllustratorsNotFound;

        var illustratorsNodes = illustratorsNode.SelectNodes(".//a");
        if (illustratorsNodes == null) goto IllustratorsNotFound;

        foreach (var illustratorNode in illustratorsNodes)
        {
            var illustrator = illustratorNode.InnerText.Trim();
            illustrators.Add(new Creator { Name = illustrator, Types = [CreatorType.Illustrator] });
        }

        Log.Debug("插画师: {Illustrators}", illustrators.Select(i => i.Name));
        progressReporter?.DebugAsync($"[DLsite/Game] 插画师: {string.Join(", ", illustrators.Select(i => i.Name))}");

        return illustrators;

        IllustratorsNotFound:
        Log.Debug("插画师未知");
        progressReporter?.DebugAsync("[DLsite/Game] 插画师: 未知");
        return null;
    }

    public List<Creator>? GetGameVoiceActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var voiceActors = new List<Creator>();
        var voiceActorsNode = htmlDocument.DocumentNode
            .SelectSingleNode("//table[@id='work_outline']//tr[th[text()='声优']]/td");
        if (voiceActorsNode == null) goto VoiceActorsNotFound;

        var voiceActorsNodes = voiceActorsNode.SelectNodes(".//a");
        if (voiceActorsNodes == null) goto VoiceActorsNotFound;

        foreach (var voiceActorNode in voiceActorsNodes)
        {
            var voiceActor = voiceActorNode.InnerText.Trim();
            voiceActors.Add(new Creator { Name = voiceActor, Types = [CreatorType.VoiceActor] });
        }

        Log.Debug("声优: {VoiceActors}", voiceActors.Select(va => va.Name));
        progressReporter?.DebugAsync($"[DLsite/Game] 声优: {string.Join(", ", voiceActors.Select(va => va.Name))}");

        return voiceActors;

        VoiceActorsNotFound:
        Log.Debug("声优未知");
        progressReporter?.DebugAsync("[DLsite/Game] 声优: 未知");
        return null;
    }

    public List<Creator>? GetGameMusicians(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var musicians = new List<Creator>();
        var musiciansNode = htmlDocument.DocumentNode
            .SelectSingleNode("//table[@id='work_outline']//tr[th[text()='音乐']]/td");
        if (musiciansNode == null) goto MusiciansNotFound;

        var musiciansNodes = musiciansNode.SelectNodes(".//a");
        if (musiciansNodes == null) goto MusiciansNotFound;

        foreach (var musicianNode in musiciansNodes)
        {
            var musician = musicianNode.InnerText.Trim();
            musicians.Add(new Creator { Name = musician, Types = [CreatorType.Musician] });
        }

        Log.Debug("音乐: {Musicians}", musicians.Select(m => m.Name));
        progressReporter?.DebugAsync($"[DLsite/Game] 音乐: {string.Join(", ", musicians.Select(m => m.Name))}");

        return musicians;

        MusiciansNotFound:
        Log.Debug("音乐未知");
        progressReporter?.DebugAsync("[DLsite/Game] 音乐: 未知");
        return null;
    }

    public List<Creator>? GetGameAuthors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var authors = new List<Creator>();
        var authorsNode = htmlDocument.DocumentNode
            .SelectSingleNode("//table[@id='work_outline']//tr[th[text()='作者']]/td");
        if (authorsNode == null) goto AuthorsNotFound;

        var authorsNodes = authorsNode.SelectNodes(".//a");
        if (authorsNodes == null) goto AuthorsNotFound;

        foreach (var authorNode in authorsNodes)
        {
            var author = authorNode.InnerText.Trim();
            authors.Add(new Creator { Name = author, Types = [CreatorType.Author] });
        }

        Log.Debug("作者: {Authors}", authors.Select(a => a.Name));
        progressReporter?.DebugAsync($"[DLsite/Game] 作者: {string.Join(", ", authors.Select(a => a.Name))}");

        return authors;

        AuthorsNotFound:
        Log.Debug("作者未知");
        progressReporter?.DebugAsync("[DLsite/Game] 作者: 未知");
        return null;
    }
}
