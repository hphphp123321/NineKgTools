using System.Globalization;
using System.Text.RegularExpressions;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.ScraperInterfaces;
using NineKgTools.Utils;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

public partial class DLsiteService : IBaseMediaScraper
{
    public Category GetCategory(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var categoryNode = htmlDocument.DocumentNode.SelectSingleNode("//tr[th='作品形式']/td/div");
        var workTypeNode = htmlDocument.DocumentNode.SelectSingleNode("//tr[th='文件形式']/td");

        if (categoryNode != null && workTypeNode != null)
        {
            // 提取并输出作品类型的文本
            var categoryText = categoryNode.InnerText.Trim();
            var workTypeText = workTypeNode.InnerText.Trim();

            Log.Debug("作品类型: {Category}, 文件类型: {WorkType}", categoryText, workTypeText);
            progressReporter?.DebugAsync($"[DLsite] 作品类型: {categoryText}, 文件类型: {workTypeText}");

            // 根据作品类型返回对应的Category
            return GetCategoryByText(categoryText, workTypeText);
        }

        if (categoryNode != null)
        {
            // 提取并输出作品类型的文本
            var categoryText = categoryNode.InnerText.Trim();

            Log.Debug("作品类型: {Category}", categoryText);
            progressReporter?.DebugAsync($"[DLsite] 作品类型: {categoryText}");

            // 根据作品类型返回对应的Category
            return GetCategoryByText(categoryText);
        }

        Log.Debug("作品类型: 未知");
        progressReporter?.DebugAsync("[DLsite] 作品类型: 未知");
        return StaticCategories.Unknown;
    }

    public (string title, List<string> aliasTitles) GetTitle(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var metaNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
        string titleText = System.Net.WebUtility.HtmlDecode(metaNode.GetAttributeValue("content", ""));

        titleText = TrimTitle(titleText);

        Log.Debug("标题: {Title}", titleText);
        progressReporter?.DebugAsync($"[DLsite] 标题: {titleText}");

        // TODO 判断语言并翻译（寻找<link rel="alternate" hreflang="xxx"中有没有官方中文）
        var aliasTitles = new List<string>();

        return (titleText, aliasTitles);
    }

    public DateTime? GetReleaseDate(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var saleDateNode =
            htmlDocument.DocumentNode.SelectSingleNode("//table[@id='work_outline']//tr[th[text()='发售日']]/td");

        // 输出贩卖日
        if (saleDateNode != null)
        {
            string dateText = saleDateNode.InnerText.Trim();
            // 尝试转换日期格式
            if (DateTime.TryParse(dateText, new CultureInfo("zh-CN"), DateTimeStyles.None, out var saleDate))
            {
                Log.Debug("发售日: {ReleaseDate}", saleDate);
                progressReporter?.DebugAsync($"[DLsite] 发售日: {saleDate:yyyy-MM-dd}");
                return saleDate;
            }
        }

        Log.Debug("发售日: 未知");
        progressReporter?.DebugAsync("[DLsite] 发售日: 未知");
        return null;
    }

    public (string summary, string? summaryTranslated) GetSummary(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var summaryNode =
            htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:description']");

        if (summaryNode != null)
        {
            string summary = System.Net.WebUtility.HtmlDecode(summaryNode.GetAttributeValue("content", "")).Trim();
            // 裁剪掉末尾的"「DLsite"
            if (summary.Contains("「DLsite"))
            {
                summary = summary.Substring(0, summary.IndexOf("「DLsite", StringComparison.Ordinal));
            }

            Log.Debug("简介: {Summary}", summary);
            progressReporter?.DebugAsync($"[DLsite] 简介: {(summary.Length > 50 ? summary.Substring(0, 50) + "..." : summary)}");

            // TODO 判断语言并翻译
            return (summary, null);
        }

        Log.Debug("简介: 未知");
        progressReporter?.DebugAsync("[DLsite] 简介: 未知");
        return ("暂无简介", null);
    }

    public (string description, string? descriptionTranslated) GetDescription(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // 获取<div itemprop="description" class="work_parts_container">中的所有html格式的内容
        var descriptionNode = htmlDocument.DocumentNode.SelectSingleNode("//div[@itemprop='description']");
        if (descriptionNode != null)
        {
            var description = descriptionNode.InnerHtml.Trim();
            Log.Debug("描述（一部分）: {Description}...", description.Substring(0, Math.Min(50, description.Length)));
            progressReporter?.DebugAsync($"[DLsite] 描述: {description.Substring(0, Math.Min(50, description.Length))}...");

            // TODO 判断语言并翻译
            return (description, null);
        }

        progressReporter?.DebugAsync("[DLsite] 描述: 未知");
        return ("暂无简介", null);
    }

    public async Task<List<Tag>> GetTagsAsync(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        var tagsNode =
            htmlDocument.DocumentNode.SelectSingleNode("//table[@id='work_outline']//tr[th[text()='分类']]/td");
        if (tagsNode == null) goto TagNotFound;

        var tagNodes = tagsNode.SelectNodes(".//a");

        if (tagNodes == null) goto TagNotFound;

        var tagList = new List<Tag>();
        foreach (var tagNode in tagNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tagText = tagNode.InnerText.Trim();

            // 通过标签名获取标签（使用异步方法）
            var tag = await _tagService.GetTagByNameAsync(tagText);
            if (tag != null)
            {
                tagList.Add(tag);
            }
            else
            {
                Log.Debug("标签{}未知", tagText);
            }
        }

        Log.Debug("标签: {Tags}", tagList.Select(tag => tag.Name).ToList());
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[DLsite] 标签: {string.Join(", ", tagList.Select(tag => tag.Name))}");
        }

        return tagList;

        TagNotFound:
        Log.Debug("标签: 未知");
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[DLsite] 标签: 未知");
        }
        return new List<Tag>();
    }

    public long GetMediaSize(MediaSource source, HtmlDocument? htmlDocument, IProgressReporter? progressReporter = null)
    {
        if (htmlDocument == null)
        {
            var size = source.GetSize();
            progressReporter?.DebugAsync($"[DLsite] 媒体大小（本地）: {FileSizeFormatter.FormatFileSize(size)}");
            return size;
        }

        var tagsNode =
            htmlDocument.DocumentNode.SelectSingleNode("//table[@id='work_outline']//tr[th[text()='文件容量']]/td");
        var sizeText = tagsNode?.InnerText.Trim();

        if (sizeText != null)
        {
            var parsedSize = FileSizeFormatter.ParseFileSize(sizeText);
            progressReporter?.DebugAsync($"[DLsite] 媒体大小: {sizeText}");
            return parsedSize;
        }

        var localSize = source.GetSize();
        progressReporter?.DebugAsync($"[DLsite] 媒体大小（本地）: {FileSizeFormatter.FormatFileSize(localSize)}");
        return localSize;
    }

    public List<Uri> GetLinks(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var originalLink = GetOriginalLink(htmlDocument);
        if (originalLink == null) goto LinkNotFound;

        var links = new List<Uri> { originalLink };
        progressReporter?.DebugAsync($"[DLsite] 链接: {originalLink}");
        return links;

        LinkNotFound:
        Log.Debug("链接: 未知");
        progressReporter?.DebugAsync("[DLsite] 链接: 未知");
        return new List<Uri>();
    }

    public Image? GetPoster(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var posterNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        if (posterNode == null) goto PosterNotFound;

        var posterUrl = posterNode.GetAttributeValue("content", "");
        if (posterUrl == "") goto PosterNotFound;

        progressReporter?.DebugAsync($"[DLsite] 封面: {posterUrl}");
        return GetImageByUrl(posterUrl);

        PosterNotFound:
        Log.Debug("封面: 未知");
        progressReporter?.DebugAsync("[DLsite] 封面: 未知");
        return null;
    }

    public List<Image> GetPictures(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        var picturesNodes =
            htmlDocument.DocumentNode.SelectNodes(
                "//div[@ref='product_slider_data' and @class='product-slider-data']/div[@data-src]");
        if (picturesNodes == null) goto PicturesNotFound;
        var images = new List<Image>();
        foreach (var pictureNode in picturesNodes)
        {
            var pictureUrl = pictureNode.GetAttributeValue("data-src", "");
            if (string.IsNullOrEmpty(pictureUrl)) continue;

            images.Add(GetImageByUrl(pictureUrl));
        }

        progressReporter?.DebugAsync($"[DLsite] 图片数量: {images.Count}");
        return images;


        PicturesNotFound:
        Log.Debug("图片: 未知");
        progressReporter?.DebugAsync("[DLsite] 图片: 未知");
        return new List<Image>();
    }

    public float GetRating(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        // 根据配置项决定是否用Selenium获取评分
        if (!_config.Website.DLsite.UseSeleniumForRating)
        {
            progressReporter?.DebugAsync("[DLsite] 评分: 跳过（未启用Selenium）");
            return 0;
        }

        // 因为DLsite是采用vue的动态数据加载，所以无法直接获取评分，需要通过Selenium工具获取
        var originalLink = GetOriginalLink(htmlDocument);
        if (originalLink == null) goto RatingNotFound;

        try
        {
            progressReporter?.DebugAsync("[DLsite] 正在通过Selenium获取评分...");
            var driver = _http.GetNewChromeDriver();
            driver.Navigate().GoToUrl(originalLink.ToString());

            // 设置显式等待
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));
            var element = wait.Until(ExpectedConditions.ElementIsVisible(By.ClassName("average_count")));
            if (element == null) goto RatingNotFound;
            var ratingText = element.Text;
            driver.Quit();
            var rating = float.Parse(ratingText);
            progressReporter?.DebugAsync($"[DLsite] 评分: {rating}");
            return rating;
        }
        catch (Exception e)
        {
            Log.Error(e, "获取评分失败");
            progressReporter?.WarningAsync($"[DLsite] 获取评分失败: {e.Message}");
            return 0;
        }


        RatingNotFound:
        Log.Debug("评分: 未知");
        progressReporter?.DebugAsync("[DLsite] 评分: 未知");
        return 0;
    }

    public Dictionary<string, string> Infos(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        progressReporter?.DebugAsync("[DLsite] 额外信息: 无");
        return new Dictionary<string, string>();
    }

    public List<string> GetRelatedMediaNames(HtmlDocument htmlDocument, IProgressReporter? progressReporter = null)
    {
        progressReporter?.DebugAsync("[DLsite] 相关作品: 无");
        return new List<string>(); // TODO 获取相关作品，并关联至数据库
    }

    private Uri? GetOriginalLink(HtmlDocument htmlDocument)
    {
        var originalLinkNode = htmlDocument.DocumentNode.SelectSingleNode("//meta[@property='og:url']");
        if (originalLinkNode == null) return null;
        var originalLink = originalLinkNode.GetAttributeValue("content", "");
        return originalLink == "" ? null : new Uri(originalLink);
    }

    public Category GetCategoryByText(string? categoryText)
    {
        if (categoryText == null)
        {
            return StaticCategories.Unknown;
        }

        if (categoryText.Contains("视频"))
        {
            return StaticCategories.HAnime;
        }

        if (categoryText.Contains("ASMR") || categoryText.Contains("音声"))
        {
            return StaticCategories.Asmr;
        }

        if (categoryText.Contains("漫画") || categoryText.Contains("单行本"))
        {
            return StaticCategories.Manga;
        }

        if (categoryText.Contains("角色扮演"))
        {
            return StaticCategories.RpgGame;
        }

        if (categoryText.Contains("模拟"))
        {
            return StaticCategories.SlgGame;
        }

        if (categoryText.Contains("动作"))
        {
            return StaticCategories.ActGame;
        }

        if (categoryText.Contains("冒险"))
        {
            return StaticCategories.AvgGame;
        }

        if (categoryText.Contains("射击"))
        {
            return StaticCategories.StgGame;
        }

        return StaticCategories.Unknown;
    }

    public Category GetCategoryByText(string categoryText, string workTypeText)
    {
        workTypeText = workTypeText.ToUpper();

        if (workTypeText.Contains("MP4") || workTypeText.Contains("WMV") || workTypeText.Contains("MPEG"))
        {
            return StaticCategories.HAnime; // 默认为H动画 TODO 更细分为同人动画
        }

        if (workTypeText.Contains("MP3") || workTypeText.Contains("WAV")) // TODO 可能有更多类型
        {
            if (categoryText.Contains("ASMR") || categoryText.Contains("音声"))
            {
                return StaticCategories.Asmr;
            }

            return StaticCategories.OtherAudio;
        }

        if (workTypeText.Contains("PDF")) // TODO 可能有更多类型
        {
            if (categoryText.Contains("漫画") || categoryText.Contains("单行本"))
            {
                return StaticCategories.Manga;
            }

            return StaticCategories.OtherPicture;
        }

        if (workTypeText.Contains("软件")) // TODO 可能有更多类型
        {
            if (categoryText.Contains("角色扮演"))
            {
                return StaticCategories.RpgGame;
            }

            if (categoryText.Contains("模拟"))
            {
                return StaticCategories.SlgGame;
            }

            if (categoryText.Contains("动作"))
            {
                return StaticCategories.ActGame;
            }

            if (categoryText.Contains("冒险"))
            {
                return StaticCategories.AvgGame;
            }

            if (categoryText.Contains("射击"))
            {
                return StaticCategories.StgGame;
            }

            return StaticCategories.OtherGame;
        }


        return StaticCategories.Unknown;
    }

    public Image GetImageByUrl(string url)
    {
        if (url.StartsWith("//")) // 加上前缀
        {
            url = "https:" + url;
        }

        return new Image(new Uri(url));
    }

    [GeneratedRegex("genre/(\\d+)/")]
    private static partial Regex DLsiteTagRegex();

    /// <summary>
    /// 去除标题中的多余部分
    /// </summary>
    public string TrimTitle(string title)
    {
        if (title.Contains(" | DLsite"))
        {
            title = title[..title.IndexOf(" | DLsite", StringComparison.Ordinal)];
        }

        // 正则匹配【xx%OFF】, 【期間限定xxx円】
        title = TitleTrimRegex().Replace(title, "");

        return title;
    }

    // 匹配【xx%OFF】、[xxx制作组]、【期間限定xxx円】、&lt;xxx&gt;
    [GeneratedRegex(@"【\d+%OFF】| \[.*\]|【期間限定\d+円】|<.*>")]
    private static partial Regex TitleTrimRegex();
}
