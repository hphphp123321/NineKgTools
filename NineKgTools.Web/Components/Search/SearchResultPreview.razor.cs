using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Search;
using NineKgTools.Utils;

namespace NineKgTools.Components.Search;

public partial class SearchResultPreview : ComponentBase
{
    [Inject] private NavigationManager Navigation { get; set; } = null!;
    
    [Parameter] public GlobalSearchResult? SearchResult { get; set; }
    [Parameter] public EventCallback OnViewAllResults { get; set; }
    [Parameter] public EventCallback<string> OnItemClick { get; set; }
    [Parameter] public int MaxItemsPerSection { get; set; } = 3;
    
    /// <summary>
    /// 获取媒体海报URL
    /// </summary>
    protected string GetMediaPosterUrl(MediaBase media)
    {
        // 使用 Poster 属性获取图片URL
        if (media.Poster != null)
        {
            return media.Poster.GetImageUrl();
        }
        return StaticStrings.ImageNotFound;
    }
    
    /// <summary>
    /// 获取高亮文本
    /// </summary>
    protected string GetHighlightedText(string originalText, List<HighlightSnippet> highlights)
    {
        if (highlights == null || !highlights.Any())
            return originalText;
        
        var highlightText = highlights.FirstOrDefault(h => h.HighlightedText != null)?.HighlightedText;
        return highlightText ?? originalText;
    }
    
    /// <summary>
    /// 获取匹配类型颜色
    /// </summary>
    protected Color GetMatchTypeColor(SearchMatchType matchType)
    {
        return matchType switch
        {
            SearchMatchType.Exact => Color.Success,
            SearchMatchType.Fuzzy => Color.Warning,
            SearchMatchType.Contains => Color.Info,
            SearchMatchType.Vector => Color.Secondary,
            SearchMatchType.Alias => Color.Primary,
            SearchMatchType.Description => Color.Tertiary,
            _ => Color.Default
        };
    }
    
    /// <summary>
    /// 获取匹配类型文本
    /// </summary>
    protected string GetMatchTypeText(SearchMatchType matchType)
    {
        return matchType switch
        {
            SearchMatchType.Exact => "精确",
            SearchMatchType.Fuzzy => "模糊",
            SearchMatchType.Contains => "包含",
            SearchMatchType.Vector => "语义",
            SearchMatchType.Alias => "别名",
            SearchMatchType.Description => "描述",
            _ => "其他"
        };
    }
    
    /// <summary>
    /// 处理项目点击事件
    /// </summary>
    protected async Task HandleItemClick(string itemId, string itemType)
    {
        if (OnItemClick.HasDelegate)
        {
            await OnItemClick.InvokeAsync($"{itemType}:{itemId}");
        }
    }
    
    /// <summary>
    /// 导航到媒体详情页面
    /// </summary>
    protected void NavigateToMedia(int mediaId)
    {
        Navigation.NavigateTo($"/media/{mediaId}");
        Navigation.Refresh();
    }
    
    /// <summary>
    /// 导航到标签页面
    /// </summary>
    protected void NavigateToTag(int tagId)
    {
        Navigation.NavigateTo($"/tag/{tagId}");
    }
    
    /// <summary>
    /// 导航到社团页面
    /// </summary>
    protected void NavigateToCircle(int circleId)
    {
        Navigation.NavigateTo($"/circles/{circleId}");
    }
    
    /// <summary>
    /// 导航到创作者页面
    /// </summary>
    protected void NavigateToCreator(int creatorId)
    {
        Navigation.NavigateTo($"/creators/{creatorId}");
    }
    
    /// <summary>
    /// 更新搜索结果
    /// </summary>
    public void UpdateResult(GlobalSearchResult? result)
    {
        SearchResult = result;
        StateHasChanged();
    }
}