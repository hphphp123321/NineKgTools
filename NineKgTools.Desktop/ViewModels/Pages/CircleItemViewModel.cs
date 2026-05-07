using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 社团列表卡 VM。比 Creator 简单——无 Types 枚举，仅头像 + 名字 + 媒体数。
/// </summary>
public partial class CircleItemViewModel : ObservableObject
{
    private readonly Circle _circle;
    private readonly ImageCacheService _imageCache;

    [ObservableProperty]
    private Bitmap? _avatar;

    public int Id => _circle.Id;
    public string Name => _circle.Name;
    public int MediaCount => _circle.Medias?.Count ?? 0;
    public string MediaCountText => MediaCount > 0 ? $"{MediaCount} 件作品" : "暂无作品";
    public bool HasAlias => _circle.AliasNames?.Count > 0;
    public string AliasText => _circle.AliasNames is { Count: > 0 } al
        ? string.Join("、", al)
        : "";

    public string AvatarFallback => string.IsNullOrEmpty(Name)
        ? "?"
        : Name[..Math.Min(1, Name.Length)].ToUpper();

    public CircleItemViewModel(Circle circle, ImageCacheService imageCache)
    {
        _circle = circle;
        _imageCache = imageCache;
        _ = LoadAvatarAsync();
    }

    private async Task LoadAvatarAsync()
    {
        var name = _circle.Avatar?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            Avatar = await _imageCache.GetOrLoadAsync(name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CircleItemViewModel 加载头像失败：{Id}", _circle.Id);
        }
    }
}
