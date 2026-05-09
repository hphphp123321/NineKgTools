using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体图片画廊里的单张图片 VM。包装 <see cref="Core.Models.Media.Image"/>，
/// 通过 ImageCacheService 懒加载 Bitmap。
///
/// 由 <see cref="MediaDetailViewModel.Pictures"/> 集合持有；缩略图条 + 主图区
/// 都从同一实例读 Bitmap（LRU 缓存命中 → 不重复解码）。
/// </summary>
public partial class MediaPictureItemViewModel : ObservableObject
{
    private readonly Core.Models.Media.Image _image;

    /// <summary>异步加载到的 bitmap；初始 null 时上层显示"加载中"占位。</summary>
    [ObservableProperty]
    private Bitmap? _bitmap;

    [ObservableProperty]
    private bool _isLoaded;

    /// <summary>缩略图条上的"当前选中"高亮，由父 VM 在 SelectedPictureIndex 变化时维护。</summary>
    [ObservableProperty]
    private bool _isSelected;

    public string Name => _image.Name;

    public MediaPictureItemViewModel(Core.Models.Media.Image image, ImageCacheService cache)
    {
        _image = image;
        _ = LoadAsync(cache);
    }

    private async Task LoadAsync(ImageCacheService cache)
    {
        try
        {
            if (string.IsNullOrEmpty(_image.Name)) return;
            var bmp = await cache.GetOrLoadAsync(_image.Name);
            if (bmp is null) return;
            Bitmap = bmp;
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MediaPictureItemViewModel.LoadAsync 失败：{Name}", _image.Name);
        }
    }
}
