using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Favorites;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// FavoriteSelectorDialog 视图上下文。比 TagSelectorDialog 更简单——Favorite 无 TopTag 层级，
/// 一次加载全部后客户端过滤。Choice 实例复用所以搜索过滤不丢选中态。
/// </summary>
public partial class FavoriteSelectorDialogContext : ObservableObject
{
    public bool AllowMultiSelect { get; }

    public FavoriteSelectorDialogContext(bool allowMultiSelect)
    {
        AllowMultiSelect = allowMultiSelect;
    }

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    public List<FavoriteChoiceVm> AllChoices { get; } = new();
    public ObservableCollection<FavoriteChoiceVm> FilteredChoices { get; } = new();

    public int SelectedCount => AllChoices.Count(c => c.IsSelected);
    public bool HasSelection => SelectedCount > 0;
    public string SelectionLabel => SelectedCount == 0 ? "未选择" : $"已选 {SelectedCount} 个";
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

    public void Initialize(IEnumerable<Favorite> allFavorites, IEnumerable<Favorite> initialSelected)
    {
        var initialIds = initialSelected.Select(f => f.Id).ToHashSet();
        AllChoices.Clear();
        foreach (var fav in allFavorites.OrderBy(f => f.Name))
        {
            AllChoices.Add(new FavoriteChoiceVm(fav, this, initialIds.Contains(fav.Id)));
        }
        RefreshFilter();
        RaiseSelectionChanged();
    }

    public List<Favorite> CollectSelected() =>
        AllChoices.Where(c => c.IsSelected).Select(c => c.Favorite).ToList();

    partial void OnSearchTextChanged(string value) => RefreshFilter();

    private void RefreshFilter()
    {
        var q = SearchText?.Trim() ?? "";
        FilteredChoices.Clear();
        foreach (var c in AllChoices)
        {
            if (q.Length == 0 || c.Favorite.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                FilteredChoices.Add(c);
            }
        }
        OnPropertyChanged(nameof(ShowEmpty));
    }

    internal void OnChoiceToggled(FavoriteChoiceVm choice, bool nowSelected)
    {
        if (!AllowMultiSelect && nowSelected)
        {
            foreach (var c in AllChoices)
            {
                if (!ReferenceEquals(c, choice) && c.IsSelected)
                    c.IsSelected = false;
            }
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

public partial class FavoriteChoiceVm : ObservableObject
{
    public Favorite Favorite { get; }
    private readonly FavoriteSelectorDialogContext _owner;

    public FavoriteChoiceVm(Favorite favorite, FavoriteSelectorDialogContext owner, bool initiallySelected)
    {
        Favorite = favorite;
        _owner = owner;
        _isSelected = initiallySelected;
    }

    public string DisplayName => Favorite.Name;
    public int MediaCount => Favorite.Medias?.Count ?? 0;
    public string CountText => MediaCount > 0 ? $"({MediaCount})" : "";
    public bool HasCount => MediaCount > 0;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _owner.OnChoiceToggled(this, value);
}
