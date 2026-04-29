using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using Serilog;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 简化的媒体卡片组件，支持通过MediaId异步加载或直接传入Media对象
/// </summary>
public partial class SimpleMediaCard : ComponentBase
{
    /// <summary>
    /// 媒体ID（用于异步加载）
    /// </summary>
    [Parameter]
    public int MediaId { get; set; }

    /// <summary>
    /// 直接传入的媒体对象（优先使用，如果提供则不会异步加载）
    /// </summary>
    [Parameter]
    public MediaBase? Media { get; set; }

    [Inject]
    private MediaService MediaService { get; set; } = null!;

    private MediaBase? _media;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadMediaAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // 如果直接传入了Media对象，使用它
        if (Media != null)
        {
            _media = Media;
            _isLoading = false;
            return;
        }

        // 当MediaId参数变化时重新加载
        if (_media == null || _media.Id != MediaId)
        {
            await LoadMediaAsync();
        }
    }

    /// <summary>
    /// 加载媒体信息
    /// </summary>
    private async Task LoadMediaAsync()
    {
        // 如果直接传入了Media对象，使用它
        if (Media != null)
        {
            _media = Media;
            _isLoading = false;
            return;
        }

        // 如果没有有效的MediaId，不加载
        if (MediaId <= 0)
        {
            _isLoading = false;
            return;
        }

        _isLoading = true;
        try
        {
            _media = await MediaService.GetSimpleMediaAsync(MediaId);
        }
        catch (Exception ex)
        {
            // 记录错误但不抛出，让UI显示错误状态
            Log.Error("加载媒体 {MediaId} 失败: {ExMessage}", MediaId, ex.Message);
            _media = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 获取媒体类型对应的颜色
    /// </summary>
    private Color GetMediaColor(TopCategory topCategory)
    {
        return topCategory switch
        {
            TopCategory.Game => Color.Primary,
            TopCategory.Audio => Color.Success,
            TopCategory.Video => Color.Warning,
            TopCategory.Picture => Color.Secondary,
            TopCategory.Text => Color.Info,
            _ => Color.Default
        };
    }
}

