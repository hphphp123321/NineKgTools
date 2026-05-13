using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// CircleSelectorDialog 视图上下文 —— **强制单选**版（每个 media 必须有且仅有一个社团）。
///
/// 与 CreatorSelectorDialogContext 同款架构（搜索 + ToggleButton WrapPanel），区别：
/// - 没有 TypeOptions（Circle 没有 CreatorType 这种枚举筛选维度）
/// - 没有"清空确认"语义：单选场景下空选无意义，PrimaryButton 必须选中一项才可用（CanSubmit = SelectedCount == 1）
/// - 不提供新建入口：编辑 Circle 内容（含创建）请去 CirclesPage 做；本对话框只解决"为这个 media 挑社团"
/// </summary>
public partial class CircleSelectorDialogContext : ObservableObject
{
    public CircleSelectorDialogContext() { }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>是否被服务端 maxResults=50 截断</summary>
    [ObservableProperty]
    private bool _isTruncated;

    public ObservableCollection<CircleChoiceVm> Choices { get; } = new();

    /// <summary>已选 Circle Id（跨 Load 持久化以便切换搜索词后保留选中态）</summary>
    private int? _selectedId;

    /// <summary>已选 Circle 实例 —— dialog 返回值来源</summary>
    private Circle? _selectedCircle;

    public bool HasSelection => _selectedCircle is not null;

    public string SelectionLabel => _selectedCircle is null
        ? "未选择"
        : $"已选：{_selectedCircle.Name}";

    public bool CanSubmit => _selectedCircle is not null;

    public bool ShowEmpty => !IsLoading && Choices.Count == 0;

    public void Initialize(Circle? initialSelected)
    {
        _selectedId = initialSelected?.Id;
        _selectedCircle = initialSelected;
        RaiseSelectionChanged();
    }

    public void ApplyResults(IReadOnlyList<Circle> results, bool isTruncated)
    {
        Choices.Clear();
        foreach (var c in results)
        {
            Choices.Add(new CircleChoiceVm(c, this, _selectedId == c.Id));
        }
        IsTruncated = isTruncated;
        OnPropertyChanged(nameof(ShowEmpty));
    }

    public Circle? CollectSelected() => _selectedCircle;

    internal void OnChoiceToggled(CircleChoiceVm choice, bool nowSelected)
    {
        if (nowSelected)
        {
            // 单选语义：切换前清掉其他 chip 的 IsSelected（视觉同步）
            foreach (var c in Choices)
            {
                if (!ReferenceEquals(c, choice) && c.IsSelected)
                    c.IsSelected = false;
            }
            _selectedId = choice.Circle.Id;
            _selectedCircle = choice.Circle;
        }
        else
        {
            // 单选场景下"取消"不应该让 _selectedCircle 变 null（强制必须选一个才能确认）
            // 但用户体验上：点同一个已选 chip → 视觉允许取消，CanSubmit 自动变 false 阻止确认
            if (_selectedId == choice.Circle.Id)
            {
                _selectedId = null;
                _selectedCircle = null;
            }
        }
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionLabel));
        OnPropertyChanged(nameof(CanSubmit));
    }
}

/// <summary>单个 Circle 的 chip VM</summary>
public partial class CircleChoiceVm : ObservableObject
{
    public Circle Circle { get; }
    private readonly CircleSelectorDialogContext _owner;

    public CircleChoiceVm(Circle circle, CircleSelectorDialogContext owner, bool initiallySelected)
    {
        Circle = circle;
        _owner = owner;
        _isSelected = initiallySelected;
    }

    public string DisplayName => Circle.Name;

    /// <summary>别名 hint：取前 2 个别名拼成 "alias1 / alias2"，多于 2 个加 "+N" 后缀</summary>
    public string AliasHint
    {
        get
        {
            if (Circle.AliasNames is null || Circle.AliasNames.Count == 0) return "";
            var head = Circle.AliasNames.Take(2);
            var prefix = string.Join(" / ", head);
            return Circle.AliasNames.Count > 2 ? $"{prefix} +{Circle.AliasNames.Count - 2}" : prefix;
        }
    }

    public bool HasAliasHint => !string.IsNullOrEmpty(AliasHint);

    /// <summary>作品数 hint —— Circle.Medias 已经 Include 进来（GetAllCircles / SearchCircles 没 Include Medias 时为 0）</summary>
    public string MediaCountHint => Circle.Medias is { Count: > 0 } ? $"{Circle.Medias.Count} 部" : "";

    public bool HasMediaCountHint => Circle.Medias is { Count: > 0 };

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _owner.OnChoiceToggled(this, value);
    }
}
