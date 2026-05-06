using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// MediaFilterDialog 视图上下文。3 段筛选：分类 / 收藏与评分 / 日期。
///
/// 桌面端的简化（与 Web 端的差异）：
/// 1) 分类段直接内联渲染（TopCategory chip + 子分类 chip）——避免嵌套对话框
/// 2) 标签段用 ComboBox 选 AllTags.Name——避免嵌套 TagSelectorDialog
/// </summary>
public partial class MediaFilterDialogContext : ObservableObject
{
    /// <summary>限制 TopCategory 选项（Unknown=允许全部）</summary>
    public TopCategory FilterTopCategory { get; }

    public MediaFilterDialogContext(
        TopCategory filterTopCategory,
        IEnumerable<Tag> allTags,
        IEnumerable<Favorite> allFavorites)
    {
        FilterTopCategory = filterTopCategory;

        TopCategoryOptions = new ObservableCollection<TopCategoryChoice>
        {
            new(TopCategory.Video, "视频"),
            new(TopCategory.Audio, "音频"),
            new(TopCategory.Picture, "图片"),
            new(TopCategory.Text, "文本"),
            new(TopCategory.Game, "游戏"),
        };
        if (filterTopCategory != TopCategory.Unknown)
        {
            for (var i = TopCategoryOptions.Count - 1; i >= 0; i--)
                if (TopCategoryOptions[i].Value != filterTopCategory)
                    TopCategoryOptions.RemoveAt(i);
        }

        TagNameOptions = new ObservableCollection<string?>(
            new[] { (string?)null }.Concat(allTags.Select(t => t.Name).OrderBy(n => n)));

        FavoriteNameOptions = new ObservableCollection<string?>(
            new[] { (string?)null }.Concat(allFavorites.Select(f => f.Name).OrderBy(n => n)));

        DateFilterTypeOptions = new ObservableCollection<DateFilterTypeChoice>
        {
            new("ReleaseDate", "发售日期"),
            new("StoreDate", "入库日期"),
            new("LastOpenDate", "最后打开"),
        };
        _selectedDateFilterType = DateFilterTypeOptions[0];

        RatingOptions = new ObservableCollection<RatingChoice>
        {
            new(null, "不限评分"),
            new(1f, "1 星及以上"),
            new(2f, "2 星及以上"),
            new(3f, "3 星及以上"),
            new(4f, "4 星及以上"),
            new(5f, "5 星"),
        };
        _selectedRating = RatingOptions[0];
    }

    // ===== 分类段 =====

    public ObservableCollection<TopCategoryChoice> TopCategoryOptions { get; }

    public ObservableCollection<CategoryChoiceVm> SubCategoryChoices { get; } = new();

    [ObservableProperty]
    private TopCategoryChoice? _selectedTopCategory;

    [ObservableProperty]
    private bool _onlyTopCategory;

    public bool ShowSubCategorySection =>
        !OnlyTopCategory && SelectedTopCategory is not null;

    public string SubCategoryHeader
    {
        get
        {
            var n = SubCategoryChoices.Count(c => c.IsSelected);
            return n > 0 ? $"具体分类（已选 {n}）" : "具体分类";
        }
    }

    // ===== 标签段（单选） =====

    public ObservableCollection<string?> TagNameOptions { get; }

    [ObservableProperty]
    private string? _selectedTagName;

    // ===== 收藏 + 评分段 =====

    public ObservableCollection<string?> FavoriteNameOptions { get; }

    [ObservableProperty]
    private string? _selectedFavoriteName;

    public ObservableCollection<RatingChoice> RatingOptions { get; }

    [ObservableProperty]
    private RatingChoice _selectedRating;

    // ===== 日期段 =====

    public ObservableCollection<DateFilterTypeChoice> DateFilterTypeOptions { get; }

    [ObservableProperty]
    private DateFilterTypeChoice _selectedDateFilterType;

    [ObservableProperty]
    private DateTimeOffset? _startDate;

    [ObservableProperty]
    private DateTimeOffset? _endDate;

    // ===== Initialize / Reset =====

    public void Initialize(
        bool onlyTop,
        TopCategory selectedTopFilter,
        IReadOnlyList<Category> selectedCategories,
        string? selectedTagName,
        string? selectedFavoriteName,
        float? minRating,
        string dateFilterType,
        DateTime? startDate,
        DateTime? endDate)
    {
        OnlyTopCategory = onlyTop;

        TopCategoryChoice? top = null;
        if (selectedTopFilter != TopCategory.Unknown)
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == selectedTopFilter);
        else if (selectedCategories.Count > 0)
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == selectedCategories[0].TopCategory);
        else if (FilterTopCategory != TopCategory.Unknown)
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == FilterTopCategory);
        SelectedTopCategory = top;

        var initialIds = selectedCategories.Select(c => c.Id).ToHashSet();
        foreach (var ch in SubCategoryChoices)
            if (initialIds.Contains(ch.Category.Id))
                ch.IsSelected = true;

        SelectedTagName = selectedTagName;
        SelectedFavoriteName = selectedFavoriteName;

        SelectedRating = RatingOptions.FirstOrDefault(r => r.Value == minRating)
                         ?? RatingOptions[0];

        SelectedDateFilterType = DateFilterTypeOptions
                                     .FirstOrDefault(t => t.Value == dateFilterType)
                                 ?? DateFilterTypeOptions[0];

        StartDate = startDate.HasValue ? new DateTimeOffset(startDate.Value) : null;
        EndDate = endDate.HasValue ? new DateTimeOffset(endDate.Value) : null;
    }

    public void Reset()
    {
        OnlyTopCategory = false;
        SelectedTopCategory = null;
        SelectedTagName = null;
        SelectedFavoriteName = null;
        SelectedRating = RatingOptions[0];
        SelectedDateFilterType = DateFilterTypeOptions[0];
        StartDate = null;
        EndDate = null;
    }

    public List<Category> CollectSelectedCategories() =>
        SubCategoryChoices.Where(c => c.IsSelected).Select(c => c.Category).ToList();

    // ===== TopCategory 切换：重算子分类列表 =====

    partial void OnSelectedTopCategoryChanged(TopCategoryChoice? value)
    {
        SubCategoryChoices.Clear();
        if (value is null)
        {
            OnPropertyChanged(nameof(ShowSubCategorySection));
            return;
        }

        foreach (var cat in StaticCategories.CategoryList
                     .Where(c => c.TopCategory == value.Value && c.TopCategory != TopCategory.Unknown)
                     .OrderBy(c => c.Id))
        {
            SubCategoryChoices.Add(new CategoryChoiceVm(cat, OnSubCategoryToggled));
        }
        OnPropertyChanged(nameof(ShowSubCategorySection));
        OnPropertyChanged(nameof(SubCategoryHeader));
    }

    partial void OnOnlyTopCategoryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSubCategorySection));
    }

    /// <summary>
    /// CategoryChoiceVm.IsSelected 切换时回调（CategoryChoiceVm 是 CategorySelectorDialogContext 的，
    /// 这里复用它，回调走 owner 的 OnChoiceToggled——为了兼容这里统一接口）。
    /// </summary>
    internal void OnSubCategoryToggled() =>
        OnPropertyChanged(nameof(SubCategoryHeader));
}

public sealed record DateFilterTypeChoice(string Value, string DisplayName);

public sealed record RatingChoice(float? Value, string DisplayName);
