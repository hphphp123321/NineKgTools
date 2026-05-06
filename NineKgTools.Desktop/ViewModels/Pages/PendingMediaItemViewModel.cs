using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 待处理 Tab 里每一行的 VM。承载 MediaSource + 可选的 MediaBase（待入库 Tab 才有）。
/// </summary>
public partial class PendingMediaItemViewModel : ObservableObject
{
    public MediaSource Source { get; }

    /// <summary>待入库 Tab：识别得到的媒体（来自 PendingIdentification.MediaBaseJson 反序列化）</summary>
    public MediaBase? IdentifiedMedia { get; }

    public int SourceId => Source.Id;
    public string FullPath => Source.FullPath;
    public string DisplayName => Source.GetFileName();
    public bool IsFolder => Source.IsFolder;
    public TopCategory TopCategory => Source.PossibleTopCategory;

    /// <summary>路径精简：开头 + … + 末尾，避免长路径撑爆行</summary>
    public string PathPreview
    {
        get
        {
            if (string.IsNullOrEmpty(FullPath)) return "";
            if (FullPath.Length <= 60) return FullPath;
            return FullPath[..20] + " … " + FullPath[^36..];
        }
    }

    public string SizeText
    {
        get
        {
            try
            {
                var bytes = Source.GetSize();
                return bytes switch
                {
                    >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
                    >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
                    >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
                    _ => $"{bytes} B"
                };
            }
            catch { return ""; }
        }
    }

    /// <summary>已识别媒体的标题（待入库 Tab 用），未识别返回 null</summary>
    public string? IdentifiedTitle => IdentifiedMedia?.Title;

    /// <summary>已识别媒体的评分文本</summary>
    public string IdentifiedRatingText =>
        IdentifiedMedia is { Rating: > 0 } ? $"★ {IdentifiedMedia.Rating:F1}" : "";

    public bool HasIdentifiedRating => IdentifiedMedia is { Rating: > 0 };

    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);
    public string CategoryDisplayName => TopCategoryStyles.DisplayName(TopCategory);

    /// <summary>选中状态（多选批量操作）</summary>
    [ObservableProperty]
    private bool _isSelected;

    public PendingMediaItemViewModel(MediaSource source, MediaBase? identifiedMedia = null)
    {
        Source = source;
        IdentifiedMedia = identifiedMedia;
    }
}
