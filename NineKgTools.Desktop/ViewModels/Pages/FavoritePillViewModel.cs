using Avalonia.Media;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// MediaDetailWindow Hero 区收藏夹 pill 的单条 VM。Name + 由 Name hash 派生的三色 SolidColorBrush
/// （Background 浅 fill / Border 实色 / Foreground accent 文字），由 <see cref="FavoriteGradientHelper"/>
/// 缓存——同名字段在不同媒体复用同一 brush 实例，主题切换需重建 VM。
///
/// 与「分类 chip」视觉风格一致（淡色 fill + 1px accent border + accent fg），
/// 不再用渐变背景 / 阴影 / 白字 —— 桌面端 Win11 风格不该有那种"卡片化"装饰。
/// </summary>
public sealed class FavoritePillViewModel
{
    public string Name { get; }
    public IBrush Background { get; }
    public IBrush BorderBrush { get; }
    public IBrush Foreground { get; }

    public FavoritePillViewModel(string name)
    {
        Name = name;
        var triplet = FavoriteGradientHelper.Get(name);
        Background = triplet.Background;
        BorderBrush = triplet.Border;
        Foreground = triplet.Foreground;
    }
}
