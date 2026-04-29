using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Tasks.Interfaces;
using System.Threading;

namespace NineKgTools.Core.Services.Websites;

public interface IWebsite
{
    /// <summary>
    /// 网站名
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// 即该网站负责哪几个顶层分类
    /// </summary>
    public List<TopCategory> TopCategories { get; }
    
    /// <summary>
    /// 代表是否启用，与设置Config相关联
    /// </summary>
    public bool Enable { get; }

    /// <summary>
    /// 根据媒体源获取媒体信息
    /// </summary>
    /// <param name="mediaSource">媒体源，包含顶层分类，媒体路径等基本信息</param>
    /// <returns></returns>
    public MediaBase? GetMediaInfo(MediaSource mediaSource);
    
    /// <summary>
    /// 异步获取媒体信息
    /// </summary>
    /// <param name="mediaSource"></param>
    /// <param name="progressReporter">进度报告器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<MediaBase?> GetMediaInfoAsync(MediaSource mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 手动直接通过每个网站对应的媒体id获取媒体信息。例如DLsite: RJ01081508, Bgm: 22905
    /// </summary>
    /// <param name="id">媒体所在网站对应的id</param>
    /// <param name="mediaSource">想要手动识别的媒体源</param>
    /// <param name="progressReporter">进度报告器（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public Task<MediaBase?> GetMediaInfoAsync(string id, MediaSource? mediaSource, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 异步搜索文件
    /// </summary>
    /// <param name="mediaSource">媒体源</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>媒体及相关程度（标题的相似度）</returns>
    public Task<PriorityQueue<MediaSearchResult, double>> SearchMediaAsync(MediaSource mediaSource, CancellationToken cancellationToken = default);
}