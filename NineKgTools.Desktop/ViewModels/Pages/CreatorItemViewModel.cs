using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 创作者列表卡 VM。承载 Creator + 异步加载头像。
/// </summary>
public partial class CreatorItemViewModel : ObservableObject
{
    private readonly Creator _creator;
    private readonly ImageCacheService _imageCache;

    [ObservableProperty]
    private Bitmap? _avatar;

    public int Id => _creator.Id;
    public string Name => _creator.Name;
    public int MediaCount => _creator.Medias?.Count ?? 0;
    public string MediaCountText => MediaCount > 0 ? $"{MediaCount} 件作品" : "暂无作品";

    /// <summary>类型标签（角色/画师/声优等），最多前 2 个，UI 当 chip 显示</summary>
    public IReadOnlyList<string> TopTypes => (_creator.Types ?? new List<CreatorType>())
        .Take(2)
        .Select(t => t switch
        {
            CreatorType.Author => "作者",
            CreatorType.Illustrator => "画师",
            CreatorType.Musician => "音乐",
            CreatorType.ScreenWriter => "编剧",
            CreatorType.VoiceActor => "声优",
            CreatorType.Director => "导演",
            CreatorType.Actor => "演员",
            _ => t.ToString()
        })
        .ToList();

    public bool HasTopTypes => TopTypes.Count > 0;

    /// <summary>头像缺失时显示首字母 placeholder</summary>
    public string AvatarFallback => string.IsNullOrEmpty(Name)
        ? "?"
        : Name[..Math.Min(1, Name.Length)].ToUpper();

    public CreatorItemViewModel(Creator creator, ImageCacheService imageCache)
    {
        _creator = creator;
        _imageCache = imageCache;
        _ = LoadAvatarAsync();
    }

    private async Task LoadAvatarAsync()
    {
        var name = _creator.Avatar?.Name;
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            Avatar = await _imageCache.GetOrLoadAsync(name);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CreatorItemViewModel 加载头像失败：{Id}", _creator.Id);
        }
    }
}
