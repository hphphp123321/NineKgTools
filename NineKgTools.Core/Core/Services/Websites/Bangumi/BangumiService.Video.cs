using System.Text.Json;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 把Bangumi的条目信息转为动画媒体
    /// </summary>
    private async Task<VideoMedia> ConvertSubjectInfoToVideoMedia(MediaBase mediaBase, SubjectInfo subjectInfo, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Video] 转换动画: {subjectInfo.Name}");
        }

        var animationMedia = new VideoMedia(mediaBase);

        // 默认全部为HAnime
        animationMedia.Category = StaticCategories.HAnime;

        if (subjectInfo.Infobox.Remove("话数", out var episode))
        {
            if (episode is JsonElement episodeString)
            {
                animationMedia.Episodes = int.Parse(episodeString.ToString());
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"[Bangumi/Video] 话数: {animationMedia.Episodes}");
                }
            }
        }

        // 获取开发人员
        var persons = await GetSubjectPersonsById(subjectInfo.Id, progressReporter, cancellationToken);
        foreach (var person in persons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var personDetail = await GetPersonDetailById(person.Id, progressReporter, cancellationToken);
            switch (person.Relation)
            {
                case "企画":
                    animationMedia.Circle = personDetail?.ToCircle();
                    if (progressReporter != null && personDetail != null)
                    {
                        await progressReporter.DebugAsync($"[Bangumi/Video] 企画: {personDetail.Name}");
                    }
                    break;
                case "制作" or "动画制作":
                    if (personDetail != null)
                    {
                        animationMedia.Makers?.Add(personDetail.ToCircle());
                    }

                    break;
                case "导演" or "剪辑":
                    if (personDetail != null)
                    {
                        animationMedia.Directors?.Add(personDetail.ToCreator());
                    }

                    break;
                case "编剧" or "脚本" or "原作":
                    if (personDetail != null)
                    {
                        animationMedia.ScreenWriters?.Add(personDetail.ToCreator());
                    }

                    break;
                case "人物原案" or "人物设计" or "人物设定" or "分镜" or "原画" or "第二原画" or "作画监督" or "色彩设计" or "色彩指定" or "背景美术"
                    or "补间动画":
                    if (personDetail != null)
                    {
                        animationMedia.Illustrators?.Add(personDetail.ToCreator());
                    }

                    break;
                case "音乐" or "音响" or "录音":
                    if (personDetail != null)
                    {
                        animationMedia.Musicians?.Add(personDetail.ToCreator());
                    }

                    break;
                default:
                    Log.Warning("未知的人员关系{Relation}", person.Relation);
                    break;
            }
        }

        // 获取声优
        var characters = await GetSubjectCharactersById(subjectInfo.Id, progressReporter, cancellationToken);
        foreach (var character in characters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var actor in character.Actors)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var personDetail = await GetPersonDetailById(actor.Id, progressReporter, cancellationToken);
                if (personDetail == null) continue;
                personDetail.Relation = "声优";
                var voiceActor = personDetail.ToCreator();
                voiceActor.Name = actor.Name;
                animationMedia.Actors?.Add(voiceActor);
            }
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Video] 声优: {animationMedia.Actors?.Count ?? 0}人");
        }

        // 填充Infobox
        AddMediaInfos(animationMedia, subjectInfo.Infobox);

        return animationMedia;
    }
}