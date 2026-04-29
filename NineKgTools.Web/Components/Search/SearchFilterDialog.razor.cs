using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Categories;
using NineKgTools.Core.Services.Tags;
using Serilog;

namespace NineKgTools.Components.Search;

public partial class SearchFilterDialog : ComponentBase
{
    [Inject] protected CategoryService CategoryService { get; set; } = null!;
    [Inject] protected TagService TagService { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public GlobalSearchOptions InitialOptions { get; set; } = new();

    // 当前筛选选项
    public GlobalSearchOptions CurrentOptions { get; set; } = new();

    // 分类相关 - 支持多选
    protected List<Category> _selectedCategories = new();
    protected bool _onlyTopCategory = false;
    protected TopCategory _selectedTopCategory = TopCategory.Unknown;
    protected FilterMode _categoryFilterMode = FilterMode.Union;

    // 标签相关
    protected List<Tag> _selectedTags = new();
    protected FilterMode _tagFilterMode = FilterMode.Union;
    protected bool _isTagSelectorVisible = false;

    // 评分相关
    public float _minRating = 0f;
    public float _maxRating = 10f;

    protected override async Task OnInitializedAsync()
    {
        // 复制初始选项
        CurrentOptions = CloneOptions(InitialOptions);

        // 从选项中恢复UI状态
        await RestoreUIFromOptions();
    }

    /// <summary>
    /// 切换实体类型
    /// </summary>
    protected void ToggleEntityType(SearchEntityTypes entityType)
    {
        if (CurrentOptions.EntityTypes.HasFlag(entityType))
        {
            CurrentOptions.EntityTypes &= ~entityType;
        }
        else
        {
            CurrentOptions.EntityTypes |= entityType;
        }

        // 至少保留一个类型
        if (CurrentOptions.EntityTypes == SearchEntityTypes.None)
        {
            CurrentOptions.EntityTypes = entityType;
        }

        StateHasChanged();
    }

    #region 分类选择

    /// <summary>
    /// 打开分类选择对话框
    /// </summary>
    protected async Task OpenCategorySelector()
    {
        var parameters = new DialogParameters
        {
            ["FilterTopCategory"] = TopCategory.Unknown, // 不限制顶级分类
            ["InitialSelectedTopCategory"] = _onlyTopCategory ? _selectedTopCategory : (TopCategory?)null,
            ["InitialSelectedCategories"] = _selectedCategories,
            ["InitialOnlyTopCategory"] = _onlyTopCategory
        };

        var options = new DialogOptions
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Small,
            CloseButton = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<CategorySelectorDialog>("选择分类", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is CategoryFilterResult categoryResult)
        {
            _onlyTopCategory = categoryResult.OnlyTopCategory;
            _selectedTopCategory = categoryResult.SelectedTopCategory;
            _selectedCategories = categoryResult.SelectedCategories;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 清除分类筛选
    /// </summary>
    protected void ClearCategoryFilter()
    {
        _onlyTopCategory = false;
        _selectedTopCategory = TopCategory.Unknown;
        _selectedCategories.Clear();
        StateHasChanged();
    }

    /// <summary>
    /// 移除单个分类
    /// </summary>
    protected void RemoveCategory(Category category)
    {
        _selectedCategories.Remove(category);
        StateHasChanged();
    }

    /// <summary>
    /// 是否有分类筛选
    /// </summary>
    protected bool HasCategoryFilter => _onlyTopCategory
        ? _selectedTopCategory != TopCategory.Unknown
        : _selectedCategories.Count > 0;

    /// <summary>
    /// 获取分类筛选显示文本
    /// </summary>
    protected string GetCategoryDisplayText()
    {
        if (_onlyTopCategory && _selectedTopCategory != TopCategory.Unknown)
        {
            return _selectedTopCategory switch
            {
                TopCategory.Video => "全部视频",
                TopCategory.Audio => "全部音频",
                TopCategory.Picture => "全部图片",
                TopCategory.Text => "全部文本",
                TopCategory.Game => "全部游戏",
                _ => ""
            };
        }

        if (_selectedCategories.Count == 0) return "";
        if (_selectedCategories.Count == 1) return _selectedCategories[0].Name;
        return $"{_selectedCategories[0].Name} 等 {_selectedCategories.Count} 个分类";
    }

    #endregion

    #region 标签选择

    /// <summary>
    /// 移除标签
    /// </summary>
    protected void RemoveTag(Tag tag)
    {
        _selectedTags.Remove(tag);
        StateHasChanged();
    }

    /// <summary>
    /// 打开标签选择器
    /// </summary>
    protected void OpenTagSelector()
    {
        _isTagSelectorVisible = true;
        StateHasChanged();
    }

    /// <summary>
    /// 关闭标签选择器
    /// </summary>
    protected void CloseTagSelector()
    {
        _isTagSelectorVisible = false;
        StateHasChanged();
    }

    /// <summary>
    /// 标签选择完成
    /// </summary>
    protected void OnTagsSelected(List<Tag> selectedTags)
    {
        _selectedTags = selectedTags;
        _isTagSelectorVisible = false;
        StateHasChanged();
    }

    #endregion

    /// <summary>
    /// 获取分类图标
    /// </summary>
    protected string GetCategoryIcon(TopCategory topCategory)
    {
        return topCategory switch
        {
            TopCategory.Game => Icons.Material.Filled.SportsEsports,
            TopCategory.Picture => Icons.Material.Filled.Image,
            TopCategory.Video => Icons.Material.Filled.Movie,
            TopCategory.Audio => Icons.Material.Filled.AudioFile,
            TopCategory.Text => Icons.Material.Filled.Article,
            _ => Icons.Material.Filled.Category
        };
    }

    /// <summary>
    /// 获取分类颜色
    /// </summary>
    protected Color GetCategoryColor(TopCategory topCategory)
    {
        return topCategory switch
        {
            TopCategory.Game => Color.Success,
            TopCategory.Picture => Color.Info,
            TopCategory.Video => Color.Secondary,
            TopCategory.Audio => Color.Primary,
            TopCategory.Text => Color.Warning,
            _ => Color.Default
        };
    }

    /// <summary>
    /// 应用筛选
    /// </summary>
    protected void ApplyFilters()
    {
        // 构建筛选选项
        BuildFiltersFromUI();

        MudDialog.Close(DialogResult.Ok(CurrentOptions));
    }

    /// <summary>
    /// 重置筛选
    /// </summary>
    protected void ResetFilters()
    {
        CurrentOptions = new GlobalSearchOptions
        {
            EntityTypes = SearchEntityTypes.All,
            EnableVectorSearch = false,
            MaxResultsPerType = 20,
            MinRelevanceScore = 0.3
        };

        // 清除分类筛选
        _selectedCategories.Clear();
        _onlyTopCategory = false;
        _selectedTopCategory = TopCategory.Unknown;
        _categoryFilterMode = FilterMode.Union;

        // 清除标签筛选
        _selectedTags.Clear();
        _tagFilterMode = FilterMode.Union;

        // 重置评分
        _minRating = 0f;
        _maxRating = 10f;

        StateHasChanged();
    }

    /// <summary>
    /// 取消对话框
    /// </summary>
    protected void Cancel()
    {
        MudDialog.Cancel();
    }

    /// <summary>
    /// 从UI构建筛选选项
    /// </summary>
    protected void BuildFiltersFromUI()
    {
        // 分类筛选 - 支持多选
        if (_onlyTopCategory && _selectedTopCategory != TopCategory.Unknown)
        {
            // 按顶级分类筛选：获取该顶级分类下的所有子分类ID
            var categoryIds = StaticCategories.CategoryList
                .Where(c => c.TopCategory == _selectedTopCategory)
                .Select(c => c.Id)
                .ToList();

            if (categoryIds.Any())
            {
                CurrentOptions.CategoryFilter = new CategoryFilter
                {
                    CategoryIds = categoryIds,
                    Mode = FilterMode.Union // 按顶级分类筛选时使用 Union 模式
                };
            }
            else
            {
                CurrentOptions.CategoryFilter = null;
            }
        }
        else if (_selectedCategories.Any())
        {
            CurrentOptions.CategoryFilter = new CategoryFilter
            {
                CategoryIds = _selectedCategories.Select(c => c.Id).ToList(),
                Mode = _categoryFilterMode
            };
        }
        else
        {
            CurrentOptions.CategoryFilter = null;
        }

        // 标签筛选
        if (_selectedTags.Any())
        {
            CurrentOptions.TagFilter = new TagFilter
            {
                TagIds = _selectedTags.Select(t => t.Id).ToList(),
                Mode = _tagFilterMode
            };
        }
        else
        {
            CurrentOptions.TagFilter = null;
        }

        // 评分筛选
        if (_minRating > 0 || _maxRating < 10)
        {
            CurrentOptions.RatingFilter = new RatingFilter
            {
                MinRating = _minRating,
                MaxRating = _maxRating
            };
        }
        else
        {
            CurrentOptions.RatingFilter = null;
        }
    }

    /// <summary>
    /// 从选项恢复UI状态
    /// </summary>
    protected async Task RestoreUIFromOptions()
    {
        // 分类筛选
        if (CurrentOptions.CategoryFilter?.CategoryIds.Any() == true)
        {
            var categoryIds = CurrentOptions.CategoryFilter.CategoryIds;

            // 检查是否是按顶级分类筛选（如果所有分类都属于同一个顶级分类，且包含该顶级分类下的所有分类）
            var categories = StaticCategories.CategoryList
                .Where(c => categoryIds.Contains(c.Id))
                .ToList();

            if (categories.Any())
            {
                var topCategory = categories.First().TopCategory;
                var allCategoriesInTop = StaticCategories.CategoryList
                    .Where(c => c.TopCategory == topCategory)
                    .ToList();

                // 如果选中的分类正好是某个顶级分类下的全部分类
                if (categories.Count == allCategoriesInTop.Count &&
                    categories.All(c => c.TopCategory == topCategory))
                {
                    _onlyTopCategory = true;
                    _selectedTopCategory = topCategory;
                    _selectedCategories.Clear();
                }
                else
                {
                    _onlyTopCategory = false;
                    _selectedTopCategory = TopCategory.Unknown;
                    _selectedCategories = categories;
                }

                _categoryFilterMode = CurrentOptions.CategoryFilter.Mode;
            }
        }

        // 标签筛选
        if (CurrentOptions.TagFilter?.TagIds.Any() == true)
        {
            try
            {
                var tags = new List<Tag>();
                foreach (var tagId in CurrentOptions.TagFilter.TagIds)
                {
                    var tag = await TagService._context.Tags.FindAsync(tagId);
                    if (tag != null)
                    {
                        tags.Add(tag);
                    }
                }

                _selectedTags = tags;
                _tagFilterMode = CurrentOptions.TagFilter.Mode;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "恢复标签筛选失败");
            }
        }

        // 评分筛选
        if (CurrentOptions.RatingFilter != null)
        {
            _minRating = CurrentOptions.RatingFilter.MinRating;
            _maxRating = CurrentOptions.RatingFilter.MaxRating;
        }
    }

    /// <summary>
    /// 克隆搜索选项
    /// </summary>
    protected GlobalSearchOptions CloneOptions(GlobalSearchOptions source)
    {
        return new GlobalSearchOptions
        {
            Query = source.Query,
            EnableVectorSearch = source.EnableVectorSearch,
            EntityTypes = source.EntityTypes,
            MaxResultsPerType = source.MaxResultsPerType,
            MinRelevanceScore = source.MinRelevanceScore,
            CategoryFilter = source.CategoryFilter != null ? new CategoryFilter
            {
                CategoryIds = source.CategoryFilter.CategoryIds.ToList(),
                Mode = source.CategoryFilter.Mode
            } : null,
            TagFilter = source.TagFilter != null ? new TagFilter
            {
                TagIds = source.TagFilter.TagIds.ToList(),
                Mode = source.TagFilter.Mode
            } : null,
            RatingFilter = source.RatingFilter != null ? new RatingFilter
            {
                MinRating = source.RatingFilter.MinRating,
                MaxRating = source.RatingFilter.MaxRating
            } : null
        };
    }
}
