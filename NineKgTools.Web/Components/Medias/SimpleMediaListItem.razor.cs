using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 简化媒体列表项组件 - 通过MediaId异步加载
/// </summary>
public partial class SimpleMediaListItem : ComponentBase
{
    [Parameter]
    public int MediaId { get; set; }

    [Inject]
    private MediaService MediaService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private MediaBase? _media;
    private bool _isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadMediaAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_media == null || _media.Id != MediaId)
        {
            await LoadMediaAsync();
        }
    }

    private async Task LoadMediaAsync()
    {
        _isLoading = true;
        try
        {
            _media = await MediaService.GetSimpleMediaAsync(MediaId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载媒体 {MediaId} 失败", MediaId);
            _media = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void NavigateToMedia()
    {
        if (_media != null)
        {
            Navigation.NavigateTo(_media.GetMediaLink());
        }
    }

    private Color GetMediaColor(TopCategory category) => MediaUIHelper.GetMediaColor(category);
}

