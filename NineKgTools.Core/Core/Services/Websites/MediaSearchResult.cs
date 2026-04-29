using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Services.Websites;

/// <summary>
/// 通过搜索获得用来展示在选择界面的媒体搜索结果
/// </summary>
public class MediaSearchResult
{
    /// <summary>
    /// 搜索关键字
    /// </summary>
    public string SearchKey { get; set; }
    
    /// <summary>
    /// 搜索到媒体的标题
    /// </summary>
    public string Title { get; set; }
    
    /// <summary>
    /// 媒体的URL
    /// </summary>
    public string? Url { get; set; }
    
    /// <summary>
    /// 搜索到的媒体的ID（对应各个网站的ID）
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// 媒体分类
    /// </summary>
    public Category Category { get; set; }
    
    /// <summary>
    /// 媒体海报
    /// </summary>
    public Image? Poster { get; set; }
    
}