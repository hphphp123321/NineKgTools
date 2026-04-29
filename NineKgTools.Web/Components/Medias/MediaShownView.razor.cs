using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Categories;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Tags;
using Serilog;
using X.PagedList;

namespace NineKgTools.Components.Medias;

// 通用媒体列表展示组件：支持网格/列表切换、筛选、分页；
// 不同页面（GamePage/TagPage 等）通过 InitialQueryParameters 传入基础筛选条件
public partial class MediaShownView : ComponentBase
{
    [Inject] private MediaService MediaService { get; set; } = default!;
    [Inject] private TagService TagService { get; set; } = default!;
    [Inject] private FavoriteService FavoriteService { get; set; } = default!;
    [Inject] private CategoryService CategoryService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    /// <summary>每页数量可选值。供 UI 选择器与 localStorage 校验共用。</summary>
    public static readonly int[] AllowedPageSizes = { 12, 20, 50, 100 };

    /// <summary>localStorage key —— 跨页面/跨会话保存用户的每页数量偏好</summary>
    private const string PageSizeStorageKey = "medialib_pagesize";

    /// <summary>基础筛选条件。例如 GamePage 传 TopCategory = Game，TagPage 传 TagNames = ["某标签"]</summary>
    [Parameter, EditorRequired]
    public MediaQueryParameters InitialQueryParameters { get; set; } = new();

    // 筛选工具栏：每项控制对应 UI 是否渲染
    [Parameter] public bool ShowCategoryFilter { get; set; } = true;
    [Parameter] public bool ShowTagFilter { get; set; } = true;
    [Parameter] public bool ShowFavoriteFilter { get; set; } = true;
    [Parameter] public bool ShowRatingFilter { get; set; } = true;
    [Parameter] public bool ShowDateFilter { get; set; } = true;
    [Parameter] public bool ShowSortOptions { get; set; } = true;

    // 外观
    [Parameter] public string Title { get; set; } = "媒体列表";
    [Parameter] public string TitleIcon { get; set; } = Icons.Material.Filled.Collections;
    [Parameter] public Color TitleColor { get; set; } = Color.Primary;
    [Parameter] public int Elevation { get; set; } = 3;
    [Parameter] public string Class { get; set; } = string.Empty;
    [Parameter] public bool Simple { get; set; }
    [Parameter] public string EmptyStateText { get; set; } = "暂无媒体";
    [Parameter] public string EmptyTitle { get; set; } = "暂无媒体";
    [Parameter] public string EmptyDescription { get; set; } = "当前筛选条件下没有找到媒体";
    [Parameter] public string EmptyIcon { get; set; } = Icons.Material.Filled.ImageNotSupported;
    [Parameter] public bool DefaultGridView { get; set; } = true;
    [Parameter] public bool HideFavoriteButton { get; set; }
    [Parameter] public RenderFragment? TitleActions { get; set; }

    [Parameter] public int PageSize { get; set; } = 20;
    [Parameter] public EventCallback<bool> OnViewModeChanged { get; set; }

    private bool _isGridView = true;
    private bool _isLoading;
    private MediaQueryParameters _currentQueryParams = new();
    private IPagedList<MediaBase>? _pagedMedias;
    private int _currentPage = 1;
    private int _totalPages;
    private int _totalItemCount;
    private List<Category> _categories = new();
    private List<Tag> _allTags = new();
    private List<Favorite> _allFavorites = new();

    /// <summary>
    /// 用户当前选用的每页数量。初始 = Parameter PageSize，首次渲染后从 localStorage 覆盖。
    /// 之后所有内部逻辑（OnPageSizeChanged / ResetFilters）都以此为准，不再回退到 Parameter。
    /// </summary>
    private int _userPageSize = 20;

    /// <summary>localStorage 是否已尝试加载，避免重复触发首次加载逻辑</summary>
    private bool _pageSizeLoaded;


    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();
        _isGridView = DefaultGridView;
        _userPageSize = PageSize;

        // 复制初始参数
        _currentQueryParams = CloneQueryParameters(InitialQueryParameters);
        _currentQueryParams.PageNumber = 1;
        _currentQueryParams.PageSize = _userPageSize;

        // 加载筛选数据源
        await LoadFilterDataSources();

        // 加载首页数据
        await LoadMediasAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // 如果 InitialQueryParameters 变化，重新加载数据
        if (!AreQueryParametersEqual(_currentQueryParams, InitialQueryParameters))
        {
            _currentQueryParams = CloneQueryParameters(InitialQueryParameters);
            _currentQueryParams.PageNumber = 1;
            _currentQueryParams.PageSize = _userPageSize;
            await LoadMediasAsync();
        }
    }

    /// <summary>
    /// 首次渲染后从 localStorage 读取用户偏好。Blazor Server 在 OnInitialized 阶段
    /// 还无法访问浏览器 JS，必须等到 OnAfterRender(firstRender)。
    /// 接受一次"先按默认 20 渲染、再立刻按 localStorage 值重新渲染"的代价。
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _pageSizeLoaded)
            return;

        _pageSizeLoaded = true;

        try
        {
            var stored = await JS.InvokeAsync<string?>("localStorage.getItem", PageSizeStorageKey);
            if (int.TryParse(stored, out var size)
                && AllowedPageSizes.Contains(size)
                && size != _userPageSize)
            {
                _userPageSize = size;
                _currentQueryParams.PageSize = size;
                _currentQueryParams.PageNumber = 1;
                _currentPage = 1;
                await LoadMediasAsync();
                // LoadMediasAsync 内部已 StateHasChanged
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取每页数量偏好失败");
        }
    }

    public async Task Refresh()
    {
        await LoadMediasAsync();
        StateHasChanged();
    }

    private async Task LoadFilterDataSources()
    {
        try
        {
            // 如果显示标签筛选，加载标签
            if (ShowTagFilter)
            {
                _allTags = await TagService.GetAllTagsAsync();
            }

            // 如果显示收藏夹筛选，加载收藏夹
            if (ShowFavoriteFilter)
            {
                _allFavorites = await FavoriteService.GetAllFavoritesAsync();
            }

            // 如果显示分类筛选，加载分类
            if (ShowCategoryFilter && _currentQueryParams.TopCategory.HasValue)
            {
                _categories = StaticCategories.CategoryList
                    .Where(c => c.TopCategory == _currentQueryParams.TopCategory.Value)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载筛选数据源失败");
        }
    }

    private async Task LoadMediasAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            // 使用服务端分页查询
            _pagedMedias = MediaService.GetPagedMediaList(_currentQueryParams);
            _totalPages = _pagedMedias.PageCount;
            _totalItemCount = _pagedMedias.TotalItemCount;
            _currentPage = _pagedMedias.PageNumber;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载媒体数据失败: {@QueryParams}", _currentQueryParams);
            Snackbar.Add("加载媒体数据失败", Severity.Error);
            _pagedMedias = null;
            _totalPages = 0;
            _totalItemCount = 0;
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task OnViewModeToggled(bool isGridView)
    {
        _isGridView = isGridView;
        StateHasChanged();

        // 触发外部事件
        if (OnViewModeChanged.HasDelegate)
        {
            await OnViewModeChanged.InvokeAsync(_isGridView);
        }
    }

    private async Task OnPageChanged(int page)
    {
        if (page == _currentPage) return;

        _currentPage = page;
        _currentQueryParams.PageNumber = page;
        await LoadMediasAsync();
    }

    /// <summary>
    /// 用户切换每页数量：写入 localStorage，重置到第 1 页，重新查询
    /// </summary>
    private async Task OnPageSizeChanged(int newSize)
    {
        if (newSize == _userPageSize || !AllowedPageSizes.Contains(newSize))
            return;

        _userPageSize = newSize;
        _currentQueryParams.PageSize = newSize;
        _currentQueryParams.PageNumber = 1;
        _currentPage = 1;

        try
        {
            await JS.InvokeVoidAsync("localStorage.setItem", PageSizeStorageKey, newSize.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "保存每页数量偏好失败");
        }

        await LoadMediasAsync();
    }

    private async Task OpenFilterDialog()
    {
        var parameters = new DialogParameters
        {
            ["TopCategory"] = _currentQueryParams.TopCategory ?? TopCategory.Unknown,
            ["Categories"] = _categories,
            ["AllTags"] = _allTags,
            ["AllFavorites"] = _allFavorites,
            ["OnlyTopCategory"] = _currentQueryParams.FilterByTopCategoryOnly,
            ["SelectedTopCategoryFilter"] = _currentQueryParams.FilterByTopCategoryOnly
                ? (_currentQueryParams.TopCategory ?? TopCategory.Unknown)
                : TopCategory.Unknown,
            ["SelectedCategories"] = _currentQueryParams.Categories ?? new List<Category>(),
            ["SelectedTagName"] = _currentQueryParams.TagNames?.FirstOrDefault(),
            ["SelectedFavoriteName"] = _currentQueryParams.FavoriteNames?.FirstOrDefault(),
            ["MinRating"] = _currentQueryParams.MinRating,
            ["DateFilterType"] = GetDateFilterTypeString(_currentQueryParams.DateType),
            ["StartDate"] = _currentQueryParams.StartDate,
            ["EndDate"] = _currentQueryParams.EndDate
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<MediaFilterDialog>("筛选设置", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is MediaFilterResult filterResult)
        {
            // 应用分类筛选结果
            _currentQueryParams.FilterByTopCategoryOnly = filterResult.OnlyTopCategory;
            if (filterResult.OnlyTopCategory)
            {
                // 仅按顶级分类筛选
                _currentQueryParams.TopCategory = filterResult.SelectedTopCategoryFilter != TopCategory.Unknown
                    ? filterResult.SelectedTopCategoryFilter
                    : _currentQueryParams.TopCategory;
                _currentQueryParams.Categories = null;
                _currentQueryParams.Category = null;
            }
            else
            {
                // 多分类筛选
                _currentQueryParams.Categories = filterResult.SelectedCategories.Count > 0
                    ? filterResult.SelectedCategories
                    : null;
                _currentQueryParams.Category = filterResult.SelectedCategories.Count == 1
                    ? filterResult.SelectedCategories[0]
                    : null;
            }

            _currentQueryParams.TagNames = string.IsNullOrEmpty(filterResult.SelectedTagName)
                ? null
                : new List<string> { filterResult.SelectedTagName };
            _currentQueryParams.FavoriteNames = string.IsNullOrEmpty(filterResult.SelectedFavoriteName)
                ? null
                : new List<string> { filterResult.SelectedFavoriteName };
            _currentQueryParams.MinRating = filterResult.MinRating;
            _currentQueryParams.DateType = ParseDateFilterType(filterResult.DateFilterType);
            _currentQueryParams.StartDate = filterResult.StartDate;
            _currentQueryParams.EndDate = filterResult.EndDate;

            // 筛选变化时重置到第一页
            _currentPage = 1;
            _currentQueryParams.PageNumber = 1;

            await LoadMediasAsync();
        }
    }

    private async Task OnSortOptionChanged(MediaSortOption sortOption)
    {
        if (_currentQueryParams.SortOption == sortOption) return;

        _currentQueryParams.SortOption = sortOption;
        _currentPage = 1;
        _currentQueryParams.PageNumber = 1;
        await LoadMediasAsync();
    }

    private async Task ResetFilters()
    {
        // 保留基础筛选（InitialQueryParameters）和用户的每页数量偏好，清除用户筛选
        _currentQueryParams = CloneQueryParameters(InitialQueryParameters);
        _currentQueryParams.PageNumber = 1;
        _currentQueryParams.PageSize = _userPageSize;
        _currentPage = 1;

        await LoadMediasAsync();
        Snackbar.Add("筛选条件已重置", Severity.Info);
    }

    private MediaQueryParameters CloneQueryParameters(MediaQueryParameters source)
    {
        return new MediaQueryParameters
        {
            Name = source.Name,
            TopCategory = source.TopCategory,
            Category = source.Category,
            TagNames = source.TagNames?.ToList(),
            FavoriteNames = source.FavoriteNames?.ToList(),
            CircleId = source.CircleId,
            CreatorId = source.CreatorId,
            MinRating = source.MinRating,
            MaxRating = source.MaxRating,
            StartDate = source.StartDate,
            EndDate = source.EndDate,
            DateType = source.DateType,
            SortOption = source.SortOption,
            PageNumber = source.PageNumber,
            PageSize = source.PageSize
        };
    }

    // 忽略分页字段比较两个查询参数是否等价
    private bool AreQueryParametersEqual(MediaQueryParameters a, MediaQueryParameters b)
    {
        if (a == null || b == null) return false;

        return a.Name == b.Name &&
               a.TopCategory == b.TopCategory &&
               a.Category?.Id == b.Category?.Id &&
               ListsEqual(a.TagNames, b.TagNames) &&
               ListsEqual(a.FavoriteNames, b.FavoriteNames) &&
               a.CircleId == b.CircleId &&
               a.CreatorId == b.CreatorId;
    }

    private bool ListsEqual(List<string>? a, List<string>? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        return !a.Except(b).Any() && !b.Except(a).Any();
    }

    // 仅统计用户在 Dialog 里添加的筛选，忽略由调用方写入 InitialQueryParameters 的基础条件
    private bool HasActiveUserFilters()
    {
        // 比较当前参数和初始参数，查找用户添加的筛选
        var hasCategory = _currentQueryParams.Category != null && _currentQueryParams.Category != InitialQueryParameters.Category;
        var hasTags = _currentQueryParams.TagNames != null && !ListsEqual(_currentQueryParams.TagNames, InitialQueryParameters.TagNames);
        var hasFavorites = _currentQueryParams.FavoriteNames != null && !ListsEqual(_currentQueryParams.FavoriteNames, InitialQueryParameters.FavoriteNames);
        var hasRating = _currentQueryParams.MinRating.HasValue;
        var hasDate = _currentQueryParams.StartDate.HasValue || _currentQueryParams.EndDate.HasValue;

        return hasCategory || hasTags || hasFavorites || hasRating || hasDate;
    }

    private int GetActiveFilterCount()
    {
        int count = 0;
        if (_currentQueryParams.Category != null && _currentQueryParams.Category != InitialQueryParameters.Category) count++;
        if (_currentQueryParams.TagNames != null && !ListsEqual(_currentQueryParams.TagNames, InitialQueryParameters.TagNames)) count++;
        if (_currentQueryParams.FavoriteNames != null && !ListsEqual(_currentQueryParams.FavoriteNames, InitialQueryParameters.FavoriteNames)) count++;
        if (_currentQueryParams.MinRating.HasValue) count++;
        if (_currentQueryParams.StartDate.HasValue || _currentQueryParams.EndDate.HasValue) count++;
        return count;
    }

    private string GetDateFilterTypeString(DateFilterType? dateType)
    {
        return dateType switch
        {
            DateFilterType.ReleaseDate => "ReleaseDate",
            DateFilterType.StoreDate => "StoreDate",
            DateFilterType.LastOpenDate => "LastOpenDate",
            _ => "ReleaseDate"
        };
    }

    private DateFilterType? ParseDateFilterType(string dateTypeStr)
    {
        return dateTypeStr switch
        {
            "ReleaseDate" => DateFilterType.ReleaseDate,
            "StoreDate" => DateFilterType.StoreDate,
            "LastOpenDate" => DateFilterType.LastOpenDate,
            _ => null
        };
    }
}
