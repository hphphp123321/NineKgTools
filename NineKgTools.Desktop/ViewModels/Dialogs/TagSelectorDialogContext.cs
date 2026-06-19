using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// TagSelectorDialog 的视图上下文。一次加载全部 Tag 后客户端分组——
/// **两级浏览**（顶层分组 → 组内标签）取代原"全量 chip 墙"：标签可能数百个，
/// 一次全渲染会卡；现在左侧列分组、右侧只渲染当前组的几十个 chip。
///
/// 顶部搜索框跨全部直查（保留高级用户直搜）；<see cref="IsSearching"/> 为真时
/// 隐藏左侧分组列、右侧改显跨全部的扁平命中结果。
///
/// 选中态由 <see cref="TagChoiceVm.IsSelected"/> 自维护——Choice 实例全局唯一（AllChoices），
/// 切组 / 搜索只是换 <see cref="VisibleChoices"/> 引用的子集，选中不丢。
/// </summary>
public partial class TagSelectorDialogContext : ObservableObject
{
    public bool AllowMultiSelect { get; }

    public TagSelectorDialogContext(bool allowMultiSelect)
    {
        AllowMultiSelect = allowMultiSelect;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching))]
    [NotifyPropertyChangedFor(nameof(ShowGroups))]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private bool _isLoading;

    /// <summary>当前选中的左侧分组——驱动右侧 chip 列表（仅浏览态）。ListBox.SelectedItem 双向绑此。</summary>
    [ObservableProperty]
    private TagGroupVm? _selectedGroup;

    /// <summary>所有 TagChoice 的真源（实例复用，分组 / 搜索过滤不重建）</summary>
    public List<TagChoiceVm> AllChoices { get; } = new();

    /// <summary>左侧顶层分组列表（按 TopTag.Id 排序，null 归入"未分组"置末）</summary>
    public ObservableCollection<TagGroupVm> Groups { get; } = new();

    /// <summary>右侧当前展示的 chip：浏览态 = 当前组的标签；搜索态 = 跨全部命中</summary>
    public ObservableCollection<TagChoiceVm> VisibleChoices { get; } = new();

    /// <summary>是否处于搜索态——非空搜索词即为真。真时隐藏左侧分组、显跨全部扁平结果。</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>是否显示左侧分组列（= 非搜索态）</summary>
    public bool ShowGroups => !IsSearching;

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

    /// <summary>仅搜索态且无命中时显示空状态（浏览态分组总有标签，不显）</summary>
    public bool ShowEmpty => !IsLoading && IsSearching && VisibleChoices.Count == 0;

    /// <summary>由 dialog 加载完成后调用，初始化所有 Choice 并按 TopTag 分组。</summary>
    public void Initialize(IEnumerable<Tag> allTags, IEnumerable<Tag> initialSelected)
    {
        var initialIds = initialSelected.Select(t => t.Id).ToHashSet();
        AllChoices.Clear();
        Groups.Clear();

        foreach (var grp in allTags
                     .GroupBy(t => new { Id = t.TopTag?.Id ?? int.MaxValue, Name = t.TopTag?.Name ?? "未分组" })
                     .OrderBy(g => g.Key.Id))
        {
            var groupVm = new TagGroupVm(grp.Key.Name);
            foreach (var tag in grp.OrderBy(t => t.Name))
            {
                var choice = new TagChoiceVm(tag, this, initialIds.Contains(tag.Id)) { Group = groupVm };
                AllChoices.Add(choice);
                groupVm.Choices.Add(choice);
            }
            groupVm.RefreshSelected();
            Groups.Add(groupVm);
        }

        SelectedGroup = Groups.FirstOrDefault();
        UpdateVisible();

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfirmText));
    }

    public List<Tag> CollectSelected() =>
        AllChoices.Where(c => c.IsSelected).Select(c => c.Tag).ToList();

    partial void OnSearchTextChanged(string value) => UpdateVisible();

    partial void OnSelectedGroupChanged(TagGroupVm? value)
    {
        if (!IsSearching) UpdateVisible();
    }

    /// <summary>按当前态重算右侧 chip 列表：搜索态 = 跨全部命中，浏览态 = 当前组标签。</summary>
    private void UpdateVisible()
    {
        VisibleChoices.Clear();
        if (IsSearching)
        {
            var q = SearchText.Trim();
            foreach (var c in AllChoices)
            {
                if (c.Tag.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (c.Tag.TopTag?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    VisibleChoices.Add(c);
                }
            }
        }
        else if (SelectedGroup is not null)
        {
            foreach (var c in SelectedGroup.Choices)
                VisibleChoices.Add(c);
        }
        OnPropertyChanged(nameof(ShowEmpty));
    }

    /// <summary>由 TagChoiceVm.IsSelected 切换时回调。单选模式互斥 + 通知派生属性 + 刷新各组徽标。</summary>
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

        // 分组数（几~几十）很少，每次 toggle 全量刷新徽标足够廉价，省去 choice→group 反查的边界处理
        foreach (var g in Groups)
            g.RefreshSelected();

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfirmText));
    }
}

/// <summary>左侧顶层分组 ViewModel——名称 + 标签总数 + 组内已选计数徽标。</summary>
public partial class TagGroupVm : ObservableObject
{
    public string Name { get; }
    public List<TagChoiceVm> Choices { get; } = new();

    public TagGroupVm(string name) => Name = name;

    public int TagCount => Choices.Count;

    /// <summary>组内已选数量——驱动 accent 徽标显隐，由 owner 在 toggle 后调 <see cref="RefreshSelected"/> 更新</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedInGroup))]
    private int _selectedInGroup;

    public bool HasSelectedInGroup => SelectedInGroup > 0;

    public void RefreshSelected() => SelectedInGroup = Choices.Count(c => c.IsSelected);
}

/// <summary>每个 Tag 的 chip ViewModel——双向绑 IsChecked 实现 toggle 选中。</summary>
public partial class TagChoiceVm : ObservableObject
{
    public Tag Tag { get; }
    private readonly TagSelectorDialogContext _owner;

    /// <summary>所属分组反向引用——toggle 后刷新该组徽标用</summary>
    public TagGroupVm? Group { get; set; }

    public TagChoiceVm(Tag tag, TagSelectorDialogContext owner, bool initiallySelected)
    {
        Tag = tag;
        _owner = owner;
        _isSelected = initiallySelected;
    }

    public string DisplayName => Tag.Name;
    public string? GroupHint => Tag.TopTag?.Name;
    public bool HasGroupHint => !string.IsNullOrEmpty(GroupHint);

    /// <summary>chip 上的"/分组"提示仅搜索态显示——浏览态同组 chip 全在一组下，重复提示是噪音。
    /// 读 owner 的实时 <see cref="TagSelectorDialogContext.IsSearching"/>：每次切态 VisibleChoices 都会
    /// 清空重填→容器重建→重新求值，故无需 PropertyChanged 通知。</summary>
    public bool ShowGroupHint => HasGroupHint && _owner.IsSearching;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _owner.OnChoiceToggled(this, value);
    }
}
