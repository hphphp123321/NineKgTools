using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体图片画廊里的单张图片 VM。包装 <see cref="Core.Models.Media.Image"/>，
/// 通过 ImageCacheService 懒加载 Bitmap（已入库图片走 cache）；新加未保存的图片
/// 通过 in-memory bytes ctor 直接 set Bitmap，不走 cache 也不要求 .cache 文件存在。
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

    /// <summary>
    /// 编辑模式新加的尚未持久化的图片标记。Save 时调 ImageService.AddOrFindImagesAsync
    /// 时这些图片需要带 Content（已在 ctor 设置）；UI 上可用此标识展示"待保存"角标。
    /// </summary>
    public bool IsPendingNew { get; }

    public Core.Models.Media.Image UnderlyingImage => _image;

    public string Name => _image.Name;

    /// <summary>已入库图片：通过 ImageCacheService 异步 load .cache 文件</summary>
    public MediaPictureItemViewModel(Core.Models.Media.Image image, ImageCacheService cache)
    {
        _image = image;
        IsPendingNew = false;
        _ = LoadAsync(cache);
    }

    /// <summary>新加图片：直接拿 file picker 读出来的 byte[] 立即解码 Bitmap，
    /// 同时把 Content 存到 _image，Save 时由 ImageService.AddOrFindImagesAsync 完成入库 + cache。</summary>
    public MediaPictureItemViewModel(Core.Models.Media.Image image, byte[] inMemoryBytes)
    {
        _image = image;
        IsPendingNew = true;
        try
        {
            using var ms = new MemoryStream(inMemoryBytes);
            Bitmap = new Bitmap(ms);
            IsLoaded = true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MediaPictureItemViewModel.ctor in-memory bytes 解码失败：{Name}", _image.Name);
        }
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
