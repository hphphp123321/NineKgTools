using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// TagSelectorDialog 的视图上下文。一次加载全部 Tag 后客户端过滤——
/// 列表通常几百条上限，本地 LINQ 比每次按搜索词查 db 快且响应即时。
/// 选中态由 TagChoiceVm.IsSelected 自维护，搜索过滤不影响选中（Choice 实例复用）。
/// </summary>
public partial class TagSelectorDialogContext : ObservableObject
{
    public bool AllowMultiSelect { get; }

    public TagSelectorDialogContext(bool allowMultiSelect)
    {
        AllowMultiSelect = allowMultiSelect;
    }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>所有 TagChoice 的真源（实例复用，搜索过滤不重建）</summary>
    public List<TagChoiceVm> AllChoices { get; } = new();

    /// <summary>当前显示的 TagChoice（按 SearchText 过滤后）</summary>
    public ObservableCollection<TagChoiceVm> FilteredChoices { get; } = new();

    public int SelectedCount => AllChoices.Count(c => c.IsSelected);

    public bool HasSelection => SelectedCount > 0;

    public string SelectionLabel => SelectedCount == 0 ? "未选择" : $"已选 {SelectedCount} 个";

    /// <summary>多选模式即使 0 项也可以确认（提交空列表 = 清空）；单选模式必须选 1 项</summary>
    public bool CanSubmit => AllowMultiSelect || SelectedCount == 1;

    public string ConfirmText
    {
        get
        {
            if (!AllowMultiSelect) return "确定";
            return SelectedCount == 0 ? "清空并确定" : $"确定（{SelectedCount}）";
        }
    }

    public bool ShowEmpty => !IsLoading && FilteredChoices.Count == 0;

    /// <summary>由 dialog 加载完成后调用，初始化所有 Choice 并按 TopTag.Id 排序。</summary>
    public void Initialize(IEnumerable<Tag> allTags, IEnumerable<Tag> initialSelected)
    {
        var initialIds = initialSelected.Select(t => t.Id).ToHashSet();
        AllChoices.Clear();
        foreach (var tag in allTags
                     .OrderBy(t => t.TopTag?.Id ?? int.MaxValue)
                     .ThenBy(t => t.Name))
        {
            AllChoices.Add(new TagChoiceVm(tag, this, initialIds.Contains(tag.Id)));
        }
        RefreshFilter();
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfirmText));
    }

    public List<Tag> CollectSelected() =>
        AllChoices.Where(c => c.IsSelected).Select(c => c.Tag).ToList();

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    private void RefreshFilter()
    {
        var q = SearchText?.Trim() ?? "";
        FilteredChoices.Clear();
        foreach (var c in AllChoices)
        {
            if (q.Length == 0 || c.Tag.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                              || (c.Tag.TopTag?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                FilteredChoices.Add(c);
            }
        }
        OnPropertyChanged(nameof(ShowEmpty));
    }

    /// <summary>由 TagChoiceVm.IsSelected 切换时回调。单选模式互斥 + 通知派生属性。</summary>
    internal void OnChoiceToggled(TagChoiceVm choice, bool nowSelected)
    {
        if (!AllowMultiSelect && nowSelected)
        {
            foreach (var c in AllChoices)
            {
                if (!ReferenceEquals(c, choice) && c.IsSelected)
                    c.IsSelected = false;
            }
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfirmText));
    }
}

/// <summary>每个 Tag 的 chip ViewModel——双向绑 IsChecked 实现 toggle 选中。</summary>
public partial class TagChoiceVm : ObservableObject
{
    public Tag Tag { get; }
    private readonly TagSelectorDialogContext _owner;

    public TagChoiceVm(Tag tag, TagSelectorDialogContext owner, bool initiallySelected)
    {
        Tag = tag;
        _owner = owner;
        _isSelected = initiallySelected;
    }

    public string DisplayName => Tag.Name;
    public string? GroupHint => Tag.TopTag?.Name;
    public bool HasGroupHint => !string.IsNullOrEmpty(GroupHint);

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _owner.OnChoiceToggled(this, value);
    }
}
