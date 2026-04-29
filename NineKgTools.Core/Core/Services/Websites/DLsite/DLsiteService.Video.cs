using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.ScraperInterfaces;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

public partial class DLsiteService : IVideoMediaScraper
{
    public int GetVideoEpisodes(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // DLsite没有提供视频集数的信息
        if (source == null)
        {
            Log.Debug("未知来源，无法获取视频集数");
            progressReporter?.DebugAsync("[DLsite/Video] 集数: 未知来源");
            return 0;
        }

        var episodes = source.GetFileCount(TopCategoryExtensions.GetExtensions(TopCategory.Video));
        Log.Debug("视频集数: {Episodes}", episodes);
        progressReporter?.DebugAsync($"[DLsite/Video] 集数: {episodes}");
        return episodes;
    }

    public Circle? GetVideoCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var circleNode = htmlDocument.DocumentNode.
            SelectSingleNode("//span[@class='maker_name']");
        if (circleNode == null) goto CircleNotFound;

        var circleName = circleNode.InnerText.Trim();
        Log.Debug("制作组: {CircleName}", circleName);
        progressReporter?.DebugAsync($"[DLsite/Video] 制作组: {circleName}");
        return new Circle { Name = circleName };

        CircleNotFound:
        Log.Debug("制作组未知");
        progressReporter?.DebugAsync("[DLsite/Video] 制作组: 未知");
        return null;
    }

    public List<Circle>? GetVideoMakers(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // dlsite没有提供制作组信息
        progressReporter?.DebugAsync("[DLsite/Video] 制作商: 无");
        return null;
    }

    public List<Creator>? GetVideoScreenWriters(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
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
        progressReporter?.DebugAsync($"[DLsite/Video] 编剧: {string.Join(", ", screenWriters.Select(sw => sw.Name))}");
        return screenWriters;

        ScreenWritersNotFound:
        Log.Debug("编剧未知");
        progressReporter?.DebugAsync("[DLsite/Video] 编剧: 未知");
        return null;
    }

    public List<Creator>? GetVideoIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
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
        progressReporter?.DebugAsync($"[DLsite/Video] 插画师: {string.Join(", ", illustrators.Select(i => i.Name))}");

        return illustrators;

        IllustratorsNotFound:
        Log.Debug("插画师未知");
        progressReporter?.DebugAsync("[DLsite/Video] 插画师: 未知");
        return null;
    }

    public List<Creator>? GetVideoActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // TODO 这是爬声优的，还有演员
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
        progressReporter?.DebugAsync($"[DLsite/Video] 声优: {string.Join(", ", voiceActors.Select(va => va.Name))}");

        return voiceActors;

        VoiceActorsNotFound:
        Log.Debug("声优未知");
        progressReporter?.DebugAsync("[DLsite/Video] 声优: 未知");
        return null;
    }

    public List<Creator>? GetVideoMusicians(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // TODO 可能还有别的字段
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
        progressReporter?.DebugAsync($"[DLsite/Video] 音乐: {string.Join(", ", musicians.Select(m => m.Name))}");

        return musicians;

        MusiciansNotFound:
        Log.Debug("音乐未知");
        progressReporter?.DebugAsync("[DLsite/Video] 音乐: 未知");
        return null;
    }

    public List<Creator>? GetVideoDirectors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // TODO 导演信息
        progressReporter?.DebugAsync("[DLsite/Video] 导演: 无");
        return null;
    }
}
