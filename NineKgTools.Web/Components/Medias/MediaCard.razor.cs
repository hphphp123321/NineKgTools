using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Components.Medias;

public partial class MediaCard : ComponentBase
{
    [Parameter]
    public int MediaId { get; set; }

    [Parameter]
    public bool HideFavoriteButton { get; set; }

    [Parameter]
    public RenderFragment? CardActions { get; set; }

    [Inject]
    private MediaService MediaService { get; set; } = null!;

    private MediaBase? _media;
    private bool _isLoading = true;
    private string _mediaLink = "";
    private string _mediaName = "";
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
                _mediaLink = _media.GetMediaLink();
                _mediaName = _media.Title;
                _rating = _media.Rating;
                _circle = _media.Circle;
            }
            else
            {
                _posterUrl = StaticStrings.ImageNotFound;
                _mediaName = "未知媒体";
                _mediaLink = "";
                _rating = 0;
                _circle = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载媒体 {MediaId} 失败", MediaId);
            _media = null;
            _posterUrl = StaticStrings.ImageNotFound;
            _mediaName = "加载失败";
            _mediaLink = "";
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

    private Color GetMediaColor(TopCategory category) => MediaUIHelper.GetMediaColor(category);
}