using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Categories;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// CategorySelectorDialog 视图上下文。两种过滤模式：
/// <list type="bullet">
///   <item>OnlyTopCategory=true ：只按 TopCategory 粗粒度过滤（"全部视频"等）</item>
///   <item>OnlyTopCategory=false：选具体子分类（多选）</item>
/// </list>
/// 切换 TopCategory 时清空已选子分类（旧选项可能不属于新顶层）。
/// </summary>
public partial class CategorySelectorDialogContext : ObservableObject
{
    /// <summary>
    /// 限制显示的 TopCategory；Unknown 表示展示全部 5 个 TopCategory（让用户自由选）。
    /// MediaFilterDialog 调用时通常传当前媒体库的 TopCategory，把选择窄到那个类型下。
    /// </summary>
    public TopCategory FilterTopCategory { get; }

    public CategorySelectorDialogContext(TopCategory filterTopCategory)
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

        // 若有限制，把不允许的 Top 移除
        if (filterTopCategory != TopCategory.Unknown)
        {
            for (var i = TopCategoryOptions.Count - 1; i >= 0; i--)
            {
                if (TopCategoryOptions[i].Value != filterTopCategory)
                    TopCategoryOptions.RemoveAt(i);
            }
        }
    }

    public ObservableCollection<TopCategoryChoice> TopCategoryOptions { get; }

    public ObservableCollection<CategoryChoiceVm> SubCategoryChoices { get; } = new();

    [ObservableProperty]
    private TopCategoryChoice? _selectedTopCategory;

    [ObservableProperty]
    private bool _onlyTopCategory;

    /// <summary>由 ShowAsync 在初始化时调用。设置初值 + 重算子分类列表 + 同步选中态</summary>
    public void Initialize(
        TopCategory? initialSelectedTop,
        IReadOnlyList<Category> initialCategories,
        bool initialOnlyTop)
    {
        OnlyTopCategory = initialOnlyTop;

        TopCategoryChoice? top = null;
        if (initialSelectedTop is { } t && t != TopCategory.Unknown)
        {
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == t);
        }
        else if (initialCategories.Count > 0)
        {
            var t2 = initialCategories[0].TopCategory;
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == t2);
        }
        else if (FilterTopCategory != TopCategory.Unknown)
        {
            top = TopCategoryOptions.FirstOrDefault(o => o.Value == FilterTopCategory);
        }

        SelectedTopCategory = top; // 触发 OnSelectedTopCategoryChanged → 重建 SubCategoryChoices

        // 应用 initial 选中（若 SubCategoryChoices 已重算到当前 top 的子分类）
        var initialIds = initialCategories.Select(c => c.Id).ToHashSet();
        foreach (var ch in SubCategoryChoices)
        {
            if (initialIds.Contains(ch.Category.Id))
                ch.IsSelected = true;
        }

        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ShowSubCategorySection));
    }

    public int SelectedCount => SubCategoryChoices.Count(c => c.IsSelected);

    public bool HasSelection => SelectedCount > 0;

    public bool ShowSubCategorySection =>
        !OnlyTopCategory && SelectedTopCategory is not null;

    public string SubCategoryHeader =>
        SelectedCount > 0 ? $"选择具体分类（已选 {SelectedCount}）" : "选择具体分类";

    /// <summary>OnlyTopCategory 模式只要选了 Top 即可；非 OnlyTopCategory 必须至少 1 个子分类</summary>
    public bool CanSubmit
    {
        get
        {
            if (SelectedTopCategory is null) return false;
            return OnlyTopCategory || SelectedCount > 0;
        }
    }

    public List<Category> CollectSelectedCategories() =>
        SubCategoryChoices.Where(c => c.IsSelected).Select(c => c.Category).ToList();

    public void SelectAll()
    {
        foreach (var c in SubCategoryChoices)
            c.IsSelected = true;
    }

    public void DeselectAll()
    {
        foreach (var c in SubCategoryChoices)
            c.IsSelected = false;
    }

    partial void OnSelectedTopCategoryChanged(TopCategoryChoice? value)
    {
        SubCategoryChoices.Clear();
        if (value is null) return;

        foreach (var cat in StaticCategories.CategoryList
                     .Where(c => c.TopCategory == value.Value && c.TopCategory != TopCategory.Unknown)
                     .OrderBy(c => c.Id))
        {
            SubCategoryChoices.Add(new CategoryChoiceVm(cat, OnChoiceToggled));
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SubCategoryHeader));
        OnPropertyChanged(nameof(CanSubmit));
        OnPropertyChanged(nameof(ShowSubCategorySection));
    }

    partial void OnOnlyTopCategoryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSubCategorySection));
        OnPropertyChanged(nameof(CanSubmit));
    }

    private void OnChoiceToggled()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SubCategoryHeader));
        OnPropertyChanged(nameof(CanSubmit));
    }
}

/// <summary>
/// 子分类 chip ViewModel——CategorySelectorDialog 与 MediaFilterDialog 共用。
/// 用 Action 回调而非 owner 引用，让两个 Context 都能创建 + 监听切换。
/// </summary>
public partial class CategoryChoiceVm : ObservableObject
{
    public Category Category { get; }
    private readonly Action _onToggled;

    public CategoryChoiceVm(Category category, Action onToggled)
    {
        Category = category;
        _onToggled = onToggled;
    }

    public string DisplayName => Category.Name;

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _onToggled();
}
