using System.Collections.Generic;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Core.Models.Search;

/// <summary>
/// 全局搜索结果
/// </summary>
public class GlobalSearchResult
{
    /// <summary>
    /// 搜索查询
    /// </summary>
    public string Query { get; set; } = string.Empty;
    
    /// <summary>
    /// 搜索耗时（毫秒）
    /// </summary>
    public long ElapsedMilliseconds { get; set; }
    
    /// <summary>
    /// 媒体搜索结果
    /// </summary>
    public List<SearchResultItem<MediaBase>> MediaResults { get; set; } = new();
    
    /// <summary>
    /// 标签搜索结果
    /// </summary>
    public List<SearchResultItem<Tag>> TagResults { get; set; } = new();
    
    /// <summary>
    /// 社团搜索结果
    /// </summary>
    public List<SearchResultItem<Circle>> CircleResults { get; set; } = new();
    
    /// <summary>
    /// 创作者搜索结果
    /// </summary>
    public List<SearchResultItem<Creator>> CreatorResults { get; set; } = new();
    
    /// <summary>
    /// 总结果数
    /// </summary>
    public int TotalCount => MediaResults.Count + TagResults.Count + 
                             CircleResults.Count + CreatorResults.Count;
    
    /// <summary>
    /// 是否使用了向量搜索
    /// </summary>
    public bool UsedVectorSearch { get; set; }
    
    /// <summary>
    /// 搜索是否被取消
    /// </summary>
    public bool WasCancelled { get; set; }
    
    /// <summary>
    /// 错误信息（如果有）
    /// </summary>
    public string? ErrorMessage { get; set; }
}