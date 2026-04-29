using System.Net.Http.Headers;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

/// <summary>
/// Bangumi网站服务，通过api调用获取数据
/// </summary>
/// <param name="config"></param>
/// <param name="http"></param>
public partial class BangumiService(Config config, HttpService http, TagService tagService, MediaNameSplitterService splitterService) : IWebsite
{
    private readonly BangumiSearch _bangumiSearch = new(config, http, splitterService);
    private readonly MediaNameSplitterService _splitterService = splitterService;
    
    public string Name => "Bangumi";

    public List<TopCategory> TopCategories =>
    [
        // TopCategory.Audio, Bangumi不支持音乐类型识别
        TopCategory.Video,
        TopCategory.Game,
        TopCategory.Text,
        TopCategory.Picture
    ];

    public bool Enable => config.Website.Bangumi.Enable;


    private static string _bangumiApiUrl = "https://api.bgm.tv";

    private string apiKey => config.Website.Bangumi.ApiKey;
    private AuthenticationHeaderValue authorization => new("Bearer", apiKey);

    public MediaBase? GetMediaInfo(MediaSource mediaSource)
    {
        return GetMediaInfoAsync(mediaSource).Result;
    }

    public async Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 开始解析: {Path.GetFileName(mediaSource.FullPath)}");
        }

        var subjectInfo = await GetSubjectInfoBySource(mediaSource, progressReporter, cancellationToken);
        if (subjectInfo == null)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[Bangumi] 未找到匹配条目");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取到条目: {subjectInfo.Name}");
        }

        return await GetMediaBySubjectInfo(subjectInfo, mediaSource, progressReporter, cancellationToken);
    }

    public async Task<MediaBase?> GetMediaInfoAsync(string id, MediaSource? mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 通过ID获取: {id}");
        }

        if (!int.TryParse(id, out var subjectId))
        {
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] ID格式无效: {id}");
            }
            return null;
        }

        var subjectInfo = await GetSubjectInfoById(subjectId, progressReporter, cancellationToken);
        if (subjectInfo == null)
        {
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] 未找到ID: {id}");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取到条目: {subjectInfo.Name}");
        }

        mediaSource ??= MediaSourceFactory.Create();
        return await GetMediaBySubjectInfo(subjectInfo, mediaSource, progressReporter, cancellationToken);
    }

    public async Task<PriorityQueue<MediaSearchResult, double>> SearchMediaAsync(MediaSource mediaSource, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var searchResultList = await _bangumiSearch.SearchSubjects(mediaSource, cancellationToken);
        if (searchResultList == null) return new();

        // 提取关键词用于评分
        var name = Path.GetFileNameWithoutExtension(mediaSource.FullPath);
        var keywords = await _splitterService.ExtractKeywordsAsync(name, cancellationToken);

        // 使用新的相关性评分系统
        return _bangumiSearch.GeneratePriorityQueueWithRelevanceScoring(searchResultList, keywords);
    }
    

    #region 私有方法

    private async Task<MediaBase?> GetMediaBySubjectInfo(SubjectInfo subjectInfo, MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 转换条目信息: {subjectInfo.Name}");
        }

        var mediaBase = await ConvertSubjectInfoToMediaBaseAsync(subjectInfo, mediaSource, progressReporter, cancellationToken);

        switch (subjectInfo.Type)
        {
            case SubjectType.Animation:
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync("[Bangumi] 类型: 动画");
                }
                return await ConvertSubjectInfoToVideoMedia(mediaBase, subjectInfo, progressReporter, cancellationToken);
            case SubjectType.Book:
                if (IsManga(subjectInfo))
                {
                    if (progressReporter != null)
                    {
                        await progressReporter.DebugAsync("[Bangumi] 类型: 漫画");
                    }
                    return await ConvertSubjectInfoToPictureMedia(mediaBase, subjectInfo, progressReporter, cancellationToken);
                }

                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync("[Bangumi] 类型: 书籍");
                }
                return await ConvertSubjectInfoToTextMedia(mediaBase, subjectInfo, progressReporter, cancellationToken);
            case SubjectType.Game:
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync("[Bangumi] 类型: 游戏");
                }
                return await ConvertSubjectInfoToGameMedia(mediaBase, subjectInfo, progressReporter, cancellationToken);
            case SubjectType.Music:
                // Bangumi 未实现音乐类型的子类包装：返回 null 让调度层 fallback 到其他网站，
                // 避免下游 MediaService 按 TopCategory.Audio 强转到 AudioMedia 时 InvalidCastException。
                Log.Warning("Bangumi 匹配到 Music 条目但不支持音乐识别，返回 null 让调度继续 fallback");
                if (progressReporter != null)
                {
                    await progressReporter.WarningAsync("[Bangumi] 匹配到音乐条目，跳过");
                }
                return null;
            case SubjectType.Real:
                // 三次元条目映射到 Unknown 类别，同样缺乏子类包装，返回 null 让上层继续 fallback。
                Log.Warning("Bangumi 匹配到 Real（三次元）条目但未实现子类包装，返回 null");
                if (progressReporter != null)
                {
                    await progressReporter.WarningAsync("[Bangumi] 匹配到三次元条目，跳过");
                }
                return null;
        }

        return mediaBase;
    }


    private async Task<SubjectInfo?> GetSubjectInfoBySource(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Bangumi] 开始搜索条目");
        }

        var searchResultList = await _bangumiSearch.SearchSubjects(mediaSource, cancellationToken);
        if (searchResultList == null)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync("[Bangumi] 搜索无结果");
            }
            return null;
        }

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 搜索到 {searchResultList.Count} 条结果");
        }

        // TODO 这里强制卡分类，后续可以通过配置来选择
        foreach (var searchResult in searchResultList.ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var mediaCategory = searchResult.Category;
            if (mediaCategory.TopCategory != mediaSource.PossibleTopCategory)
            {
                searchResultList.Remove(searchResult);
            }
        }

        // 选取相似度最高的项目
        var subjectId = await _bangumiSearch.MatchSubjectIdByName(searchResultList, mediaSource.GetFileName(), cancellationToken);
        if (subjectId != 0)
        {
            if (progressReporter != null)
            {
                await progressReporter.DebugAsync($"[Bangumi] 匹配到条目ID: {subjectId}");
            }
            return await GetSubjectInfoById(subjectId, progressReporter, cancellationToken);
        }

        Log.Warning("Bangumi搜索结果为空");
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync("[Bangumi] 未匹配到合适条目");
        }
        return null;
    }



    #endregion
}