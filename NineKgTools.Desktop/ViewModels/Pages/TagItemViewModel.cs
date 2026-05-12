using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 子标签行 VM。承载 Tag + 媒体计数 + 与所属顶级标签卡片同色系的视觉 brush。
/// </summary>
public partial class TagItemViewModel : ObservableObject
{
    public Tag Source { get; }

    public int Id => Source.Id;
    public string Name => Source.Name;
    public string? Description => Source.Description;
    public int MediaCount => Source.Medias?.Count ?? 0;
    public string MediaCountText => MediaCount > 0 ? $"{MediaCount} 媒体" : "暂无媒体";
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    /// <summary>所属顶级标签名。顶级列表态全局搜索结果用——给用户标明匹配项的归属。</summary>
    public string? TopTagName => Source.TopTag?.Name;
    public bool HasTopTagName => !string.IsNullOrWhiteSpace(TopTagName);

    /// <summary>与所属顶级标签卡片同色系的 accent brush。
    /// 算法与 <see cref="TopTagItemViewModel"/> 完全一致（TopTag.Id % 5），保证两侧视觉延续。
    /// 孤儿标签（TopTag=null）返回 null，UI fallback 到系统 accent。</summary>
    public IBrush? TopTagAccentBrush { get; }
    public IBrush? TopTagFillBrush { get; }

    private static readonly string[] AccentKeys =
    {
        "BrandCategoryVideoBrush",
        "BrandCategoryAudioBrush",
        "BrandCategoryGameBrush",
        "BrandCategoryPictureBrush",
        "BrandCategoryTextBrush",
    };

    private static readonly string[] FillKeys =
    {
        "BrandCategoryVideoFillBrush",
        "BrandCategoryAudioFillBrush",
        "BrandCategoryGameFillBrush",
        "BrandCategoryPictureFillBrush",
        "BrandCategoryTextFillBrush",
    };

    public TagItemViewModel(Tag tag)
    {
        Source = tag;

        if (tag.TopTag is not null)
        {
            var idx = ((tag.TopTag.Id % 5) + 5) % 5;
            TopTagAccentBrush = ResolveBrush(AccentKeys[idx]);
            TopTagFillBrush = ResolveBrush(FillKeys[idx]);
        }
    }

    private static IBrush? ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetResource(
                key, Application.Current.ActualThemeVariant, out var obj) == true
            && obj is IBrush b)
        {
            return b;
        }
        return null;
    }
}
