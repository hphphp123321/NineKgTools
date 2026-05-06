using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// PendingMediaPreviewDialog 视图上下文。把 PendingIdentification 反序列化得到的 MediaBase 摊平
/// 成一组只读绑定字段——纯展示，没有交互式状态变化，故无需 ObservableObject。
/// </summary>
public sealed class PendingMediaPreviewDialogContext
{
    public PendingMediaPreviewDialogContext(MediaBase media, MediaSource? source)
    {
        Title = media.Title;
        SourcePath = source?.FullPath ?? media.Source?.FullPath ?? "";
        CircleName = media.Circle?.Name;
        HasCircle = !string.IsNullOrEmpty(CircleName);

        var creatorNames = media.Creators?.Select(c => c.Name).Distinct().ToList() ?? new List<string>();
        CreatorsText = string.Join(" · ", creatorNames);
        HasCreators = creatorNames.Count > 0;

        var top = media.Category?.TopCategory ?? TopCategory.Unknown;
        TopCategoryName = top switch
        {
            TopCategory.Video => "视频",
            TopCategory.Audio => "音频",
            TopCategory.Picture => "图片",
            TopCategory.Text => "文本",
            TopCategory.Game => "游戏",
            _ => "未知",
        };
        CategoryName = media.Category?.Name ?? "";

        Rating = media.Rating;
        HasRating = Rating > 0;
        RatingText = HasRating ? $"★ {Rating:F1}" : "";

        Tags = media.Tags?.Select(t => t.Name).ToList() ?? new List<string>();
        HasTags = Tags.Count > 0;

        AliasText = media.AliasTitles?.Count > 0
            ? string.Join("、", media.AliasTitles)
            : "";
        HasAlias = !string.IsNullOrEmpty(AliasText);

        ReleaseDateText = media.ReleaseDate?.ToString("yyyy-MM-dd") ?? "";
        HasReleaseDate = media.ReleaseDate.HasValue;

        Summary = string.IsNullOrWhiteSpace(media.Summary) ? "（无简介）" : media.Summary;
        Description = string.IsNullOrWhiteSpace(media.Description) ? "" : media.Description;
        HasDescription = !string.IsNullOrEmpty(Description);
    }

    public string Title { get; }
    public string SourcePath { get; }

    public string? CircleName { get; }
    public bool HasCircle { get; }

    public string CreatorsText { get; }
    public bool HasCreators { get; }

    public string TopCategoryName { get; }
    public string CategoryName { get; }

    public float Rating { get; }
    public bool HasRating { get; }
    public string RatingText { get; }

    public IReadOnlyList<string> Tags { get; }
    public bool HasTags { get; }

    public string AliasText { get; }
    public bool HasAlias { get; }

    public string ReleaseDateText { get; }
    public bool HasReleaseDate { get; }

    public string Summary { get; }
    public string Description { get; }
    public bool HasDescription { get; }
}
