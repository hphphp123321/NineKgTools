using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 顶级标签卡 VM。承载 TopTag + 子标签计数 + 视觉色系（按 Id mod 5 在 5 类别色之间循环）。
/// </summary>
public partial class TopTagItemViewModel : ObservableObject
{
    private readonly TopTag _topTag;

    public int Id => _topTag.Id;
    public string Name => _topTag.Name;
    public int ChildCount { get; }
    public string ChildCountText => ChildCount > 0 ? $"{ChildCount} 个标签" : "暂无标签";

    public IBrush? AccentBrush { get; }
    public IBrush? FillBrush { get; }

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

    public TopTagItemViewModel(TopTag topTag, int childCount)
    {
        _topTag = topTag;
        ChildCount = childCount;

        var idx = ((Id % 5) + 5) % 5;
        AccentBrush = ResolveBrush(AccentKeys[idx]);
        FillBrush = ResolveBrush(FillKeys[idx]);
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
