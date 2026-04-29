using System.Text.Json;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 把Bangumi的条目信息转为媒体基类
    /// </summary>
    private async Task<MediaBase> ConvertSubjectInfoToMediaBaseAsync(SubjectInfo subjectInfo, MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 转换基础信息: {subjectInfo.Name}");
        }

        // 转为DateTime，string格式为"2011-12-10"
        if (!DateTime.TryParse(subjectInfo.Date, out var releaseDate))
        {
            Log.Warning("转换日期{Time}失败", subjectInfo.Date);
            releaseDate = DateTime.MinValue; // 如果转换失败，设置为最小值
        }
        else
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Bangumi] 发售日期: {releaseDate:yyyy-MM-dd}");
            }
        }

        // 添加别名
        var aliasTitles = new List<string> { subjectInfo.Name_Cn };
        if (subjectInfo.Infobox.TryGetValue("别名", out var infoboxItem))
        {
            // 将value的类型转为List<Dictionary<string, string>>
            if (infoboxItem is List<object> aliasTitlesList)
            {
                foreach (JsonElement aliasTitle in aliasTitlesList)
                {
                    var dict = aliasTitle.ToStringDictionary();
                    if (dict.TryGetValue("v", out var aliasTitleValue))
                    {
                        aliasTitles.Add(aliasTitleValue);
                    }
                }
            }
        }
        Log.Debug("获取到别名：{AliasTitles}", aliasTitles);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 别名: {string.Join(", ", aliasTitles)}");
        }

        var tags = await ParseTagsAsync(subjectInfo.Tags, progressReporter, cancellationToken);

        var mediaBase = new MediaBase
        {
            Title = subjectInfo.Name,
            AliasTitles = aliasTitles, // 别名
            Category = subjectInfo.Type.ToCategory(),
            Source = mediaSource,
            Summary = subjectInfo.Summary,
            Poster = subjectInfo.Images.ToImage(),
            Tags = tags,
            StoreDate = DateTime.Now,
            ReleaseDate = releaseDate,
            Rating = (float)subjectInfo.Rating.Score/2,
            Links = ParseRelatedLinks(subjectInfo),
            Size = mediaSource.GetSize() // Bangumi没有提供文件大小
        };

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 评分: {mediaBase.Rating:F1}");
        }

        return mediaBase;
    }


    private async Task<List<Tag>> ParseTagsAsync(List<BangumiTag> bangumiTags, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        var tags = new List<Tag>();
        foreach (var bangumiTag in bangumiTags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tag = await tagService.GetTagByNameAsync(bangumiTag.Name);
            if (tag != null)
            {
                tags.Add(tag);
            }
        }

        Log.Debug("获取到标签：{Tags}", tags.Select(tag => tag.Name).ToList());
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 标签: {string.Join(", ", tags.Select(t => t.Name))}");
        }

        return tags;
    }
        
    private static void AddMediaInfos(MediaBase mediaBase, Dictionary<string, object> infobox)
    {
        
        foreach (var (key, value) in infobox)
        {
            switch (value)
            {
                case List<object> list:
                    foreach (JsonElement jsonElement in list)
                    {
                        var dict = jsonElement.ToStringDictionary();
                        // 如果key已经存在，说明是多值
                        if (mediaBase.Infos.ContainsKey(key))
                        {
                            if (dict.TryGetValue("v", out var v))
                            {
                                mediaBase.Infos[key] += $", {v}";
                            }
                        }
                        else
                        {
                            if (dict.TryGetValue("v", out var v))
                            {
                                mediaBase.Infos[key] = v;
                            }
                        }
                    } 
                    break;
                case string stringValue:
                    mediaBase.Infos[key] = stringValue;
                    break;
                case JsonElement obj:
                    // 其他类型的值，直接转为字符串
                    mediaBase.Infos[key] = obj.ToString();
                    break;
            }
        }
    }
    
    private static List<Uri> ParseRelatedLinks(SubjectInfo subjectInfo)
    {
        var bangumiUrl = $"https://bgm.tv/subject/{subjectInfo.Id}";
        
        var relatedLinks = new List<Uri>
        {
            new(bangumiUrl)
        };
        
        var infobox = subjectInfo.Infobox;
        if (infobox.TryGetValue("官方网站", out var relatedLinksItem))
        {
            if (relatedLinksItem is string relatedLink)
            {
                relatedLinks.Add(new Uri(relatedLink));
            }
        }
        if (infobox.TryGetValue("DLsite", out var relatedLinksItem2))
        {
            if (relatedLinksItem2 is string relatedLink2)
            {
                relatedLinks.Add(new Uri(relatedLink2));
            }
        }

        if (infobox.TryGetValue("链接", out var relatedLinksItem3)){
            // ValueKind = Object : "{"k":"DLsite","v":"https://www.dlsite.com/maniax/announce/=/product_id/RJ01169914.html"}"
            if (relatedLinksItem3 is List<object> relatedLink3)
            {
                foreach (JsonElement jsonElement in relatedLink3)
                {
                    var dict = jsonElement.ToStringDictionary();
                    if (dict.TryGetValue("v", out var v))
                    {
                        relatedLinks.Add(new Uri(v));
                    }
                }
            }
        }
        
        
        // TODO 可能有更多

        return relatedLinks;
    }
}