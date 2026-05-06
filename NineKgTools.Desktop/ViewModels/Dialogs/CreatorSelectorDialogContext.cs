using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// CreatorSelectorDialog 视图上下文。搜索 + 类型筛选都走 CreatorService（服务端走 db），
/// 防止把全表创作者一次性吞到客户端（用户量大时几千上万条）。
/// </summary>
public partial class CreatorSelectorDialogContext : ObservableObject
{
    public bool AllowMultiSelect { get; }

    public CreatorSelectorDialogContext(bool allowMultiSelect)
    {
        AllowMultiSelect = allowMultiSelect;

        TypeOptions = new ObservableCollection<CreatorTypeChoice>
        {
            new(null, "全部类型"),
            new(CreatorType.Author, "作者"),
            new(CreatorType.Illustrator, "画师"),
            new(CreatorType.Musician, "音乐"),
            new(CreatorType.ScreenWriter, "编剧"),
            new(CreatorType.VoiceActor, "声优"),
            new(CreatorType.Director, "导演"),
            new(CreatorType.Actor, "演员"),
        };
        _selectedType = TypeOptions[0];
    }

    /// <summary>类型筛选下拉的选项（含"全部类型"）</summary>
    public ObservableCollection<CreatorTypeChoice> TypeOptions { get; }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private CreatorTypeChoice _selectedType;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>是否被服务端 maxResults=50 截断——告知用户"还有更多匹配，请缩小范围"</summary>
    [ObservableProperty]
    private bool _isTruncated;

    /// <summary>当前显示的 CreatorChoice（每次 Load 重建）</summary>
    public ObservableCollection<CreatorChoiceVm> Choices { get; } = new();

    /// <summary>已选 Creator 的 Id（跨 Load 持久化），点击 chip 时同步更新</summary>
    private readonly HashSet<int> _selectedIds = new();

    /// <summary>已选 Creator 实例（保持插入顺序，作为 dialog 返回值）</summary>
    private readonly List<Creator> _selectedCreators = new();

    public int SelectedCount => _selectedCreators.Count;

    public bool HasSelection => SelectedCount > 0;

    public string SelectionLabel => SelectedCount == 0 ? "未选择" : $"已选 {SelectedCount} 位";

    public bool CanSubmit => AllowMultiSelect || SelectedCount == 1;

    public string ConfirmText
    {
        get
        {
            if (!AllowMultiSelect) return "确定";
            return SelectedCount == 0 ? "清空并确定" : $"确定（{SelectedCount}）";
        }
    }

    public bool ShowEmpty => !IsLoading && Choices.Count == 0;

    /// <summary>对话框打开时的初始已选——用于 Initialize 把 Id 装进 _selectedIds</summary>
    public void Initialize(IEnumerable<Creator> initialSelected)
    {
        _selectedIds.Clear();
        _selectedCreators.Clear();
        foreach (var c in initialSelected)
        {
            if (_selectedIds.Add(c.Id))
                _selectedCreators.Add(c);
        }
        RaiseSelectionChanged();
    }

    /// <summary>由 ShowAsync 在每次搜索 / 类型筛选完成后调用</summary>
    public void ApplyResults(IReadOnlyList<Creator> results, bool isTruncated)
    {
        Choices.Clear();
        foreach (var c in results)
        {
            Choices.Add(new CreatorChoiceVm(c, this, _selectedIds.Contains(c.Id)));
        }
        IsTruncated = isTruncated;
        OnPropertyChanged(nameof(ShowEmpty));
    }

    public List<Creator> CollectSelected() => _selectedCreators.ToList();

    /// <summary>由 CreatorChoiceVm.IsSelected 切换回调</summary>
    internal void OnChoiceToggled(CreatorChoiceVm choice, bool nowSelected)
    {
        if (nowSelected)
        {
            if (!AllowMultiSelect)
            {
                // 单选：先清空，再加自己
                _selectedIds.Clear();
                _selectedCreators.Clear();
                foreach (var c in Choices)
                {
                    if (!ReferenceEquals(c, choice) && c.IsSelected)
                        c.IsSelected = false;
                }
            }
            if (_selectedIds.Add(choice.Creator.Id))
                _selectedCreators.Add(choice.Creator);
        }
        else
        {
            _selectedIds.Remove(choice.Creator.Id);
            _selectedCreators.RemoveAll(c => c.Id == choice.Creator.Id);
        }
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ConfirmText));
    }
}

public sealed record CreatorTypeChoice(CreatorType? Value, string DisplayName);

/// <summary>单个 Creator 的 chip VM</summary>
public partial class CreatorChoiceVm : ObservableObject
{
    public Creator Creator { get; }
    private readonly CreatorSelectorDialogContext _owner;

    public CreatorChoiceVm(Creator creator, CreatorSelectorDialogContext owner, bool initiallySelected)
    {
        Creator = creator;
        _owner = owner;
        _isSelected = initiallySelected;
    }

    public string DisplayName => Creator.Name;

    public string TypesHint
    {
        get
        {
            if (Creator.Types == null || Creator.Types.Count == 0) return "";
            var names = Creator.Types.Take(2).Select(t => t switch
            {
                CreatorType.Author => "作者",
                CreatorType.Illustrator => "画师",
                CreatorType.Musician => "音乐",
                CreatorType.ScreenWriter => "编剧",
                CreatorType.VoiceActor => "声优",
                CreatorType.Director => "导演",
                CreatorType.Actor => "演员",
                _ => t.ToString(),
            });
            var prefix = string.Join(" / ", names);
            return Creator.Types.Count > 2 ? $"{prefix} +{Creator.Types.Count - 2}" : prefix;
        }
    }

    public bool HasTypesHint => !string.IsNullOrEmpty(TypesHint);

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _owner.OnChoiceToggled(this, value);
    }
}
