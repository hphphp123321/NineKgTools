using Microsoft.AspNetCore.Components;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 媒体卡片网格组件 - 统一的媒体卡片网格布局
/// </summary>
public partial class MediaGrid : ComponentBase
{
    /// <summary>
    /// 媒体ID列表（优先使用）
    /// </summary>
    [Parameter]
    public IEnumerable<int>? MediaIds { get; set; }

    /// <summary>
    /// 媒体对象列表（如果没有提供MediaIds，则使用此参数）
    /// </summary>
    [Parameter]
    public IEnumerable<MediaBase>? Medias { get; set; }

    /// <summary>
    /// 是否使用简化模式（SimpleMediaCard），默认true
    /// </summary>
    [Parameter]
    public bool Simple { get; set; } = true;

    /// <summary>
    /// 是否隐藏收藏按钮（仅在Simple=false时有效）
    /// </summary>
    [Parameter]
    public bool HideFavoriteButton { get; set; }

    /// <summary>
    /// 附加的CSS类名
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 是否显示空状态
    /// </summary>
    [Parameter]
    public bool ShowEmptyState { get; set; } = true;

    /// <summary>
    /// 空状态文本
    /// </summary>
    [Parameter]
    public string EmptyStateText { get; set; } = "暂无媒体";
}

