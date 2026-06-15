using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Desktop.Services;

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
    /// 孤儿标签（TopTag=null）返回 null（TagsPage 子标签行用，null = 继承默认色）。</summary>
    public IBrush? TopTagAccentBrush { get; }
    public IBrush? TopTagFillBrush { get; }

    // ===== chip 渲染专用：永不为 null =====
    // 全局搜索 / 搜索结果页把 TopTag 标签渲染成 chip。XAML 里**不能**用
    // {Binding ..., FallbackValue={DynamicResource X}}——DynamicResource 只能作用在
    // AvaloniaProperty 上，而 Binding.FallbackValue/TargetNullValue 是 CLR 属性，会报
    // "DynamicResource 仅可与依赖项属性一起使用"。这里在 VM 端用 ResourceLookup 兜底默认色，
    // XAML 直接 {Binding ChipXxxBrush} 即可，孤儿标签退化为中性 chip（与原 fallback 等价）。
    /// <summary>chip 背景填充：有 TopTag 色用色系 fill，否则中性 Subtle。</summary>
    public IBrush? ChipFillBrush { get; }
    /// <summary>chip 边框：有 TopTag 色用 accent，否则中性 ControlStroke。</summary>
    public IBrush? ChipBorderBrush { get; }
    /// <summary>chip 主文字（标签名）：有色用 accent，否则正文主色 TextPrimary。</summary>
    public IBrush? ChipNameBrush { get; }
    /// <summary>chip 次文字（媒体计数）：有色用 accent，否则正文次色 TextSecondary。</summary>
    public IBrush? ChipCountBrush { get; }

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

        IBrush? accent = null, fill = null;
        if (tag.TopTag is not null)
        {
            var idx = ((tag.TopTag.Id % 5) + 5) % 5;
            accent = ResolveBrush(AccentKeys[idx]);
            fill = ResolveBrush(FillKeys[idx]);
        }

        TopTagAccentBrush = accent;
        TopTagFillBrush = fill;

        ChipFillBrush = fill ?? ResolveBrush("SubtleFillColorTertiaryBrush");
        ChipBorderBrush = accent ?? ResolveBrush("ControlStrokeColorDefaultBrush");
        ChipNameBrush = accent ?? ResolveBrush("TextFillColorPrimaryBrush");
        ChipCountBrush = accent ?? ResolveBrush("TextFillColorSecondaryBrush");
    }

    // ResourceLookup 走 IResourceHost 扩展，沿 Styles 链搜索——FluentAvalonia 主题 brush
    // （SubtleFillColorTertiaryBrush 等）才能命中；Application.Current.Resources.TryGetResource
    // 只搜 App 字典会 miss（见 CLAUDE.md §4.8）。
    private static IBrush? ResolveBrush(string key) => ResourceLookup.Brush(key);
}
