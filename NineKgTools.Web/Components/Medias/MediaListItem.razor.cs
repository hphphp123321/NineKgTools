using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 完整媒体列表项组件
/// </summary>
public partial class MediaListItem : ComponentBase
{
    [Parameter]
    public int MediaId { get; set; }

    [Parameter]
    public bool HideFavoriteButton { get; set; }

    [Inject]
    private MediaService MediaService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private MediaBase? _media;
    private bool _isLoading = true;
    private string _posterUrl = StaticStrings.ImageNotFound;
    private Circle? _circle;
    private float _rating;

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
            _media = await MediaService.GetMediaForCardAsync(MediaId);

            if (_media != null)
            {
                _posterUrl = _media.Poster?.GetImageUrl() ?? StaticStrings.ImageNotFound;
                _rating = _media.Rating;
                _circle = _media.Circle;
            }
            else
            {
                _posterUrl = StaticStrings.ImageNotFound;
                _rating = 0;
                _circle = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载媒体列表项 {MediaId} 失败", MediaId);
            _media = null;
            _posterUrl = StaticStrings.ImageNotFound;
            _rating = 0;
            _circle = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshMedia()
    {
        await LoadMediaAsync();
        StateHasChanged();
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

