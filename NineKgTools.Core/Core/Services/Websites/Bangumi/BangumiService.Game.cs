using System.Text.Json;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 把Bangumi的条目信息转为游戏媒体
    /// </summary>
    private async Task<GameMedia> ConvertSubjectInfoToGameMedia(MediaBase mediaBase, SubjectInfo subjectInfo, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Game] 转换游戏: {subjectInfo.Name}");
        }

        var gameMedia = new GameMedia(mediaBase);

        // 平台
        if (subjectInfo.Infobox.Remove("平台", out var platform))
        {
            switch (platform)
            {
                case string platformString:
                    gameMedia.Platforms.Add(ParsePlatform(platformString));
                    break;
                case List<object> platformStrings:
                {
                    foreach (JsonElement platformStringObject in platformStrings)
                    {
                        var platformStringDict = platformStringObject.ToStringDictionary();
                        if (platformStringDict.TryGetValue("v", out var platformString))
                        {
                            gameMedia.Platforms.Add(ParsePlatform(platformString));
                        }
                    }

                    break;
                }
                default:
                    throw new Exception("平台不是字符串或字符串列表");
            }
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Bangumi/Game] 平台: {string.Join(", ", gameMedia.Platforms)}");
            }
        }

        // 游戏类型
        if (subjectInfo.Infobox.Remove("游戏类型", out var gameType))
        {
            if (gameType is JsonElement gameTypeJson)
            {
                var gameTypeString = gameTypeJson.ToString();
                gameMedia.Category = ParseCategory(gameTypeString);
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"[Bangumi/Game] 类型: {gameTypeString}");
                }
            }
            else
            {
                throw new Exception("游戏类型不是字符串");
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
                case "开发":
                    gameMedia.Circle = personDetail?.ToCircle();
                    if (progressReporter != null && personDetail != null)
                    {
                        await progressReporter.DebugAsync($"[Bangumi/Game] 开发: {personDetail.Name}");
                    }
                    break;
                case "原画":
                    if (personDetail != null)
                    {
                        gameMedia.Illustrators.Add(personDetail.ToCreator());
                    }
                    break;
                case "剧本":
                    if (personDetail != null)
                    {
                        gameMedia.ScreenWriters.Add(personDetail.ToCreator());
                    }
                    break;
                case "音乐":
                    if (personDetail != null)
                    {
                        gameMedia.Musicians.Add(personDetail.ToCreator());
                    }
                    break;
                default:
                    Log.Warning("未知的游戏开发人员关系：{Relation}", person.Relation);
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

                // 通过GetPersonDetailById来获取更加细节的信息
                var personDetail = await GetPersonDetailById(actor.Id, progressReporter, cancellationToken);
                if (personDetail == null) continue;
                personDetail.Relation = "声优";
                var voiceActor = personDetail.ToCreator();
                voiceActor.Name = actor.Name;
                gameMedia.VoiceActors?.Add(voiceActor);
            }
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Game] 声优: {gameMedia.VoiceActors?.Count ?? 0}人");
        }

        // 填充Infobox
        AddMediaInfos(gameMedia, subjectInfo.Infobox);

        return gameMedia;
    }

    private static Platform ParsePlatform(string platformString)
    {
        return platformString switch
        {
            "Windows" => Platform.Windows,
            "PC" => Platform.Windows,
            "Android" => Platform.Android,
            "iOS" => Platform.iOS,
            "Mac" => Platform.Mac,
            "Linux" => Platform.Linux,
            _ => Platform.Other
        };
    }

    private static Category ParseCategory(string categoryString)
    {
        return categoryString switch
        {
            "SLG" => StaticCategories.SlgGame,
            "SLN" => StaticCategories.SlgGame,

            "AVG" => StaticCategories.AvgGame,
            "ADV" => StaticCategories.AvgGame,

            "ACT" => StaticCategories.ActGame,
            "ACN" => StaticCategories.ActGame,

            "RPG" => StaticCategories.RpgGame,

            "STG" => StaticCategories.StgGame,

            _ => StaticCategories.OtherGame
        };
    }
}