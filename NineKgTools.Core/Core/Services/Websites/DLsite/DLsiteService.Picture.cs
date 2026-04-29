using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.ScraperInterfaces;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

public partial class DLsiteService : IPictureMediaScraper
{
    public int GetPicturePageNum(MediaSource? source, HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // DLsite没有提供图片页数的信息
        if (source == null)
        {
            Log.Debug("未知来源，无法获取图片页数");
            progressReporter?.DebugAsync("[DLsite/Picture] 页数: 未知来源");
            return 0;
        }

        var pageNum = source.GetFileCount(TopCategoryExtensions.GetExtensions(TopCategory.Picture));
        Log.Debug("图片页数: {PageNum}", pageNum);
        progressReporter?.DebugAsync($"[DLsite/Picture] 页数: {pageNum}");
        return pageNum;
    }

    public Circle? GetPictureCircle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var circleNode = htmlDocument.DocumentNode.
            SelectSingleNode("//span[@class='maker_name']");
        if (circleNode == null) goto CircleNotFound;

        var circleName = circleNode.InnerText.Trim();
        Log.Debug("出版社: {CircleName}", circleName);
        progressReporter?.DebugAsync($"[DLsite/Picture] 出版社: {circleName}");
        return new Circle { Name = circleName };

        CircleNotFound:
        Log.Debug("出版社未知");
        progressReporter?.DebugAsync("[DLsite/Picture] 出版社: 未知");
        return null;
    }

    public List<Creator>? GetPictureIllustrators(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
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

        Log.Debug("画师: {Illustrators}", illustrators.Select(i => i.Name));
        progressReporter?.DebugAsync($"[DLsite/Picture] 画师: {string.Join(", ", illustrators.Select(i => i.Name))}");

        return illustrators;

        IllustratorsNotFound:
        Log.Debug("画师未知");
        progressReporter?.DebugAsync("[DLsite/Picture] 画师: 未知");
        return null;
    }

    public List<Creator>? GetPictureActors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // DLsite没有真人图片
        progressReporter?.DebugAsync("[DLsite/Picture] 演员: 无");
        return null;
    }

    public List<Creator>? GetPictureAuthors(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
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
        progressReporter?.DebugAsync($"[DLsite/Picture] 作者: {string.Join(", ", authors.Select(a => a.Name))}");

        return authors;

        AuthorsNotFound:
        Log.Debug("作者未知");
        progressReporter?.DebugAsync("[DLsite/Picture] 作者: 未知");
        return null;
    }
}
