using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 单个 TagMapping 行 VM。IsActive 可双向切换——切换时由 owner 持久化到 db。
/// </summary>
public partial class TagMappingItemViewModel : ObservableObject
{
    public TagMapping Source { get; }
    private readonly Action<TagMappingItemViewModel, bool> _onActiveToggled;
    private bool _suppressCallback;

    public int Id => Source.Id;
    public string SourceName => Source.SourceName;
    public int Priority => Source.Priority;
    public int HitCount => Source.HitCount;
    public string? Description => Source.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string TargetTagName => Source.TargetTag?.Name ?? "—（未关联目标）";
    public string TargetTagFullPath
    {
        get
        {
            var top = Source.TargetTag?.TopTag?.Name;
            var name = Source.TargetTag?.Name;
            if (string.IsNullOrEmpty(top) && string.IsNullOrEmpty(name)) return "—";
            if (string.IsNullOrEmpty(top)) return name ?? "";
            return $"{top} / {name}";
        }
    }

    public string CreatedAtText => Source.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string LastHitText => Source.LastHitAt.HasValue
        ? Source.LastHitAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        : "—";
    public bool IsUnused => Source.HitCount == 0;

    [ObservableProperty]
    private bool _isActive;

    public TagMappingItemViewModel(TagMapping mapping, Action<TagMappingItemViewModel, bool> onActiveToggled)
    {
        Source = mapping;
        _onActiveToggled = onActiveToggled;

        _suppressCallback = true;
        IsActive = mapping.IsActive;
        _suppressCallback = false;
    }

    partial void OnIsActiveChanged(bool value)
    {
        if (_suppressCallback) return;
        _onActiveToggled(this, value);
    }
}
