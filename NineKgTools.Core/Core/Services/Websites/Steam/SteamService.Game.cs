using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Steam.Models;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Steam;

public partial class SteamService
{
    /// <summary>
    /// 将 Steam appdetails 返回的数据映射为 GameMedia
    /// </summary>
    private async Task<GameMedia> ConvertAppDataToGameMediaAsync(
        SteamAppData data,
        MediaSource mediaSource,
        IProgressReporter? progressReporter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam/Game] 转换游戏: {data.Name}");
        }

        // Summary / Description（去 HTML 标签）
        var summary = CleanHtml(data.ShortDescription) ?? "暂无简介";
        var description = CleanHtml(data.DetailedDescription ?? data.AboutTheGame) ?? "暂无描述";

        // 发售日期
        DateTime? releaseDate = ParseReleaseDate(data.ReleaseDate?.Date);
        if (progressReporter != null && releaseDate != null)
        {
            await progressReporter.DebugAsync($"[Steam/Game] 发售日期: {releaseDate:yyyy-MM-dd}");
        }

        // 海报（header_image）
        Image? poster = null;
        if (!string.IsNullOrWhiteSpace(data.HeaderImage) && Uri.TryCreate(data.HeaderImage, UriKind.Absolute, out var headerUri))
        {
            poster = new Image(headerUri);
        }

        // 截图
        var pictures = new List<Image>();
        if (data.Screenshots != null)
        {
            foreach (var ss in data.Screenshots)
            {
                var url = ss.PathFull ?? ss.PathThumbnail;
                if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var picUri))
                {
                    pictures.Add(new Image(picUri));
                }
            }
        }

        // 链接
        var storeUrl = $"https://store.steampowered.com/app/{data.SteamAppid}";
        var links = new List<Uri> { new(storeUrl) };
        if (!string.IsNullOrWhiteSpace(data.Website) && Uri.TryCreate(data.Website, UriKind.Absolute, out var siteUri))
        {
            links.Add(siteUri);
        }

        // 标签（genres + categories 合并走 TagService）
        var tags = await ParseTagsAsync(data, progressReporter, cancellationToken);

        // 评分（metacritic 0-100 → 0-5）
        var rating = 0f;
        if (data.Metacritic != null && data.Metacritic.Score > 0)
        {
            rating = data.Metacritic.Score / 20f;
        }

        // Category
        var category = data.Categories is { Count: > 0 }
            ? InferGameCategory(data.Genres, data.Categories)
            : StaticCategories.OtherGame;

        var gameMedia = new GameMedia
        {
            Title = data.Name,
            Category = category,
            Source = mediaSource,
            Summary = summary,
            Description = description,
            Poster = poster,
            Pictures = pictures,
            Links = links,
            Tags = tags,
            Rating = rating,
            StoreDate = DateTime.Now,
            ReleaseDate = releaseDate,
            Size = mediaSource.GetSize()
        };

        // Developers → Circle（首位作开发商），其余落 Infos
        if (data.Developers is { Count: > 0 })
        {
            gameMedia.Circle = new Circle { Name = data.Developers[0] };
            gameMedia.Infos["Developers"] = string.Join(", ", data.Developers);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Steam/Game] 开发商: {gameMedia.Circle.Name}");
            }
        }

        // Publishers → Infos
        if (data.Publishers is { Count: > 0 })
        {
            gameMedia.Infos["Publishers"] = string.Join(", ", data.Publishers);
        }

        // SteamAppId 落 Infos 便于后续反查
        gameMedia.Infos["SteamAppId"] = data.SteamAppid.ToString();

        // Metacritic 评分
        if (data.Metacritic != null && data.Metacritic.Score > 0)
        {
            gameMedia.Infos["Metacritic"] = data.Metacritic.Score.ToString();
        }

        // 支持平台
        if (data.Platforms != null)
        {
            if (data.Platforms.Windows) gameMedia.Platforms.Add(Platform.Windows);
            if (data.Platforms.Mac) gameMedia.Platforms.Add(Platform.Mac);
            if (data.Platforms.Linux) gameMedia.Platforms.Add(Platform.Linux);
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Steam/Game] 平台: {string.Join(", ", gameMedia.Platforms)}");
            }
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam/Game] 标签: {string.Join(", ", tags.Select(t => t.Name))}");
        }

        return gameMedia;
    }

    /// <summary>
    /// 解析 genres + categories → Tag 列表
    /// </summary>
    private async Task<List<Tag>> ParseTagsAsync(SteamAppData data, IProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        var tags = new List<Tag>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        async Task TryAdd(string name)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) return;
            var tag = await tagService.GetTagByNameAsync(name);
            if (tag != null)
            {
                tags.Add(tag);
            }
        }

        if (data.Genres != null)
        {
            foreach (var g in data.Genres)
            {
                await TryAdd(g.Description);
            }
        }

        if (data.Categories != null)
        {
            foreach (var c in data.Categories)
            {
                await TryAdd(c.Description);
            }
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Steam/Game] 解析标签 {tags.Count} 个");
        }

        return tags;
    }

    /// <summary>
    /// 推断游戏子分类：根据 genres / categories 中常见关键词映射
    /// </summary>
    private static Category InferGameCategory(List<SteamGenre>? genres, List<SteamCategory>? categories)
    {
        var names = new List<string>();
        if (genres != null) names.AddRange(genres.Select(g => g.Description ?? string.Empty));
        if (categories != null) names.AddRange(categories.Select(c => c.Description ?? string.Empty));

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var lower = name.ToLowerInvariant();

            if (lower.Contains("rpg") || lower.Contains("role-playing") || lower.Contains("角色扮演"))
                return StaticCategories.RpgGame;
            if (lower.Contains("action") || lower.Contains("动作"))
                return StaticCategories.ActGame;
            if (lower.Contains("adventure") || lower.Contains("冒险"))
                return StaticCategories.AvgGame;
            if (lower.Contains("shooter") || lower.Contains("射击"))
                return StaticCategories.StgGame;
            if (lower.Contains("simulation") || lower.Contains("strategy") || lower.Contains("模拟") || lower.Contains("策略"))
                return StaticCategories.SlgGame;
        }

        return StaticCategories.OtherGame;
    }

    /// <summary>
    /// 去除 HTML 标签，保留纯文本
    /// </summary>
    private static string? CleanHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var text = doc.DocumentNode.InnerText;
            text = System.Net.WebUtility.HtmlDecode(text);
            // 压缩多余空白
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return html;
        }
    }

    /// <summary>
    /// 解析 Steam release_date.date 字符串。格式因 language 参数而异
    /// - english: "10 Oct, 2007"
    /// - schinese: "2007 年 10 月 10 日"
    /// - japanese: "2007年10月10日"
    /// </summary>
    private static DateTime? ParseReleaseDate(string? date)
    {
        if (string.IsNullOrWhiteSpace(date)) return null;

        // 常规尝试
        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        // 英文格式 "10 Oct, 2007" / "Oct 10, 2007"
        var formats = new[]
        {
            "d MMM, yyyy",
            "d MMMM, yyyy",
            "MMM d, yyyy",
            "MMMM d, yyyy",
            "yyyy-MM-dd",
            "yyyy/MM/dd",
        };
        if (DateTime.TryParseExact(date, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        // 中文 / 日文："2007 年 10 月 10 日" / "2007年10月10日"
        var match = Regex.Match(date, @"(\d{4})\s*[年/-]\s*(\d{1,2})\s*[月/-]\s*(\d{1,2})");
        if (match.Success)
        {
            var y = int.Parse(match.Groups[1].Value);
            var m = int.Parse(match.Groups[2].Value);
            var d = int.Parse(match.Groups[3].Value);
            try { return new DateTime(y, m, d); }
            catch { return null; }
        }

        // 仅有年份 "2024"
        if (Regex.IsMatch(date, @"^\d{4}$") && int.TryParse(date, out var year))
        {
            return new DateTime(year, 1, 1);
        }

        Log.Debug("Steam 无法解析发售日期: {Date}", date);
        return null;
    }
}
