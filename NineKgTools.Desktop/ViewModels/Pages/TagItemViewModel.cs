using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 子标签行 VM。承载 Tag + 媒体计数。
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

    public TagItemViewModel(Tag tag)
    {
        Source = tag;
    }
}
