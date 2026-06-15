using Avalonia;
using Avalonia.Media;
using NineKgTools.Core.Models.Categories;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// TopCategory → 视觉样式（类别色 brush / 图标 Geometry / 中文名）的统一映射。
/// 集中在此避免在多个 ViewModel 里写重复 switch。
/// </summary>
public static class TopCategoryStyles
{
    public static string DisplayName(TopCategory cat) => cat switch
    {
        TopCategory.Video => "视频",
        TopCategory.Audio => "音频",
        TopCategory.Game => "游戏",
        TopCategory.Picture => "图片",
        TopCategory.Text => "文本",
        _ => "未分类"
    };

    /// <summary>对应 BrandResources.axaml 里的 BrandCategory{Type}Brush key</summary>
    public static string AccentBrushKey(TopCategory cat) => cat switch
    {
        TopCategory.Video => "BrandCategoryVideoBrush",
        TopCategory.Audio => "BrandCategoryAudioBrush",
        TopCategory.Game => "BrandCategoryGameBrush",
        TopCategory.Picture => "BrandCategoryPictureBrush",
        TopCategory.Text => "BrandCategoryTextBrush",
        _ => "TextFillColorTertiaryBrush"
    };

    /// <summary>对应带 alpha 的 fill brush（用于卡片浅色背景）</summary>
    public static string FillBrushKey(TopCategory cat) => cat switch
    {
        TopCategory.Video => "BrandCategoryVideoFillBrush",
        TopCategory.Audio => "BrandCategoryAudioFillBrush",
        TopCategory.Game => "BrandCategoryGameFillBrush",
        TopCategory.Picture => "BrandCategoryPictureFillBrush",
        TopCategory.Text => "BrandCategoryTextFillBrush",
        _ => "LayerFillColorDefaultBrush"
    };

    /// <summary>对应 BrandResources.axaml 里的 IconCategory{Type} StreamGeometry key</summary>
    public static string IconResourceKey(TopCategory cat) => cat switch
    {
        TopCategory.Video => "IconCategoryVideo",
        TopCategory.Audio => "IconCategoryAudio",
        TopCategory.Game => "IconCategoryGame",
        TopCategory.Picture => "IconCategoryPicture",
        TopCategory.Text => "IconCategoryText",
        _ => "IconLibrary"
    };

    /// <summary>从 Application 资源解析 brush（运行时用，避免 AXAML 写大量 if-else）</summary>
    public static IBrush? ResolveAccentBrush(TopCategory cat)
        => ResolveBrush(AccentBrushKey(cat));

    public static IBrush? ResolveFillBrush(TopCategory cat)
        => ResolveBrush(FillBrushKey(cat));

    public static Geometry? ResolveIconGeometry(TopCategory cat)
        => ResourceLookup.Geometry(IconResourceKey(cat));

    private static IBrush? ResolveBrush(string key) => ResourceLookup.Brush(key);
}
