using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class MediaOverviewViewModel : PageViewModelBase
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;
    private readonly TagService _tagService;
    private readonly FavoriteService _favoriteService;
    private readonly IServiceProvider _services;

    public override string Title => "媒体库";

    /// <summary>5 个可选顶级分类（不含 Unknown）</summary>
    public IReadOnlyList<TopCategory> AvailableCategories { get; } = new[]
    {
        TopCategory.Video, TopCategory.Audio, TopCategory.Game, TopCategory.Picture, TopCategory.Text
    };

    /// <summary>排序选项展示用：成对 (enum, 显示名)</summary>
    public IReadOnlyList<SortChoice> AvailableSorts { get; } = new[]
    {
        new SortChoice(MediaSortOption.IdDesc, "最近添加"),
        new SortChoice(MediaSortOption.IdAsc, "最早添加"),
        new SortChoice(MediaSortOption.TitleAsc, "标题 A→Z"),
        new SortChoice(MediaSortOption.TitleDesc, "标题 Z→A"),
        new SortChoice(MediaSortOption.RatingDesc, "评分高→低"),
        new SortChoice(MediaSortOption.RatingAsc, "评分低→高"),
        new SortChoice(MediaSortOption.SizeDesc, "文件大→小"),
        new SortChoice(MediaSortOption.LastOpenDateDesc, "最近打开"),
    };

    /// <summary>nullable：null 表示"全部"（不按顶级分类筛选），否则筛选指定分类</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCategoryAll))]
    [NotifyPropertyChangedFor(nameof(IsCategoryVideo))]
    [NotifyPropertyChangedFor(nameof(IsCategoryAudio))]
    [NotifyPropertyChangedFor(nameof(IsCategoryGame))]
    [NotifyPropertyChangedFor(nameof(IsCategoryPicture))]
    [NotifyPropertyChangedFor(nameof(IsCategoryText))]
    private TopCategory? _selectedCategory; // 默认 null = "全部"

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private SortChoice _selectedSort;

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _pageSize = 24; // 6×4 每屏

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _items = new();

    /// <summary>三态显式标志，由 LoadAsync 直接置位（不依赖 NotifyPropertyChangedFor 的源生成器，更可靠）</summary>
    [ObservableProperty]
    private bool _hasItems;

    [ObservableProperty]
    private bool _showEmpty = true; // 初始（未首次加载前）显示空态而不是空白窗

    [ObservableProperty]
    private bool _showError;

    // ===== 高级筛选状态（由 MediaFilterDialog 写入；见 OpenFilterAsync） =====

    /// <summary>true=只按 _filterTopCategoryOverride（粗粒度）；false=按 _filterCategories 子分类多选</summary>
    private bool _filterOnlyTopCategory;

    /// <summary>OnlyTopCategory=true 时使用的 Top 类型（覆盖 SelectedCategory，作用范围更大）</summary>
    private TopCategory? _filterTopCategoryOverride;

    /// <summary>子分类多选；OnlyTopCategory=false 时使用</summary>
    private List<Category> _filterCategories = new();

    private string? _filterTagName;
    private string? _filterFavoriteName;
    private int? _filterCircleId;       // 来自 MediaDetailWindow 社团 chip 跳转
    private string? _filterCircleName;  // 仅用于 chip 摘要文案
    private int? _filterCreatorId;      // 来自 MediaDetailWindow 创作者 chip 跳转
    private string? _filterCreatorName;
    private float? _filterMinRating;
    private DateFilterType? _filterDateType;
    private DateTime? _filterStartDate;
    private DateTime? _filterEndDate;

    // ===== 跨页跳转入口（MediaDetailWindow 点 chip 直接进入"该 tag/creator/circle 关联媒体" overview） =====

    /// <summary>由 NavigationService.configureBeforeEnter 调用——OnEnterAsync 之前注入 tag 筛选。</summary>
    public void ApplyTagFilter(string tagName)
    {
        _filterTagName = tagName;
        _hasActiveFilter = true;
        HasActiveFilter = true;
        FilterSummary = $"标签: {tagName}";
    }

    public void ApplyCreatorFilter(int creatorId, string creatorName)
    {
        _filterCreatorId = creatorId;
        _filterCreatorName = creatorName;
        _hasActiveFilter = true;
        HasActiveFilter = true;
        FilterSummary = $"创作者: {creatorName}";
    }

    public void ApplyCircleFilter(int circleId, string circleName)
    {
        _filterCircleId = circleId;
        _filterCircleName = circleName;
        _hasActiveFilter = true;
        HasActiveFilter = true;
        FilterSummary = $"社团: {circleName}";
    }

    /// <summary>是否设置了至少一项高级筛选——AXAML 用此控制"已筛选"chip + "清除筛选"按钮可见</summary>
    [ObservableProperty]
    private bool _hasActiveFilter;

    /// <summary>当前筛选条件的简短摘要（"已筛选 N 项"）</summary>
    [ObservableProperty]
    private string _filterSummary = "";

    private void RecomputeStates()
    {
        HasItems = Items.Count > 0;
        ShowEmpty = !IsLoading && LoadError is null && Items.Count == 0;
        ShowError = !IsLoading && LoadError is not null;
    }

    // 给 AXAML 简化绑定用——避免在 XAML 写 enum 比较 converter
    public bool IsCategoryAll => SelectedCategory is null;
    public bool IsCategoryVideo => SelectedCategory == TopCategory.Video;
    public bool IsCategoryAudio => SelectedCategory == TopCategory.Audio;
    public bool IsCategoryGame => SelectedCategory == TopCategory.Game;
    public bool IsCategoryPicture => SelectedCategory == TopCategory.Picture;
    public bool IsCategoryText => SelectedCategory == TopCategory.Text;

    public MediaOverviewViewModel(
        MediaService mediaService,
        ImageCacheService imageCache,
        TagService tagService,
        FavoriteService favoriteService,
        IServiceProvider services)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        _tagService = tagService;
        _favoriteService = favoriteService;
        _services = services;
        _selectedSort = AvailableSorts[0];
    }

    public override Task OnEnterAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        LoadError = null;
        RecomputeStates();

        try
        {
            // 高级筛选优先：若 OnlyTopCategory=true 且设了 override，用它；否则用顶级 chip 选中的
            var effectiveTop = _filterOnlyTopCategory && _filterTopCategoryOverride.HasValue
                ? _filterTopCategoryOverride
                : SelectedCategory;

            var parameters = new MediaQueryParameters
            {
                TopCategory = effectiveTop,
                FilterByTopCategoryOnly = _filterOnlyTopCategory || _filterCategories.Count == 0,
                Categories = _filterCategories.Count > 0 ? _filterCategories : null,
                Name = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                TagNames = string.IsNullOrEmpty(_filterTagName) ? null : new List<string> { _filterTagName },
                FavoriteNames = string.IsNullOrEmpty(_filterFavoriteName) ? null : new List<string> { _filterFavoriteName },
                CircleId = _filterCircleId,
                CreatorId = _filterCreatorId,
                MinRating = _filterMinRating,
                DateType = _filterDateType,
                StartDate = _filterStartDate,
                EndDate = _filterEndDate,
                SortOption = SelectedSort.Option,
                PageNumber = PageNumber,
                PageSize = PageSize,
            };

            // 同步调（DbContext 非线程安全）；分页 24 条 + AsNoTracking + Includes
            var paged = _mediaService.GetPagedMediaList(parameters);
            var cards = paged.Select(m => new MediaCardViewModel(m, _imageCache)).ToList();

            Items = new ObservableCollection<MediaCardViewModel>(cards);
            TotalCount = paged.TotalItemCount;
            TotalPages = paged.PageCount;

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaOverview 加载失败");
            LoadError = "加载失败，请稍后重试。";
            Items = new ObservableCollection<MediaCardViewModel>();
            TotalCount = 0;
            TotalPages = 0;
        }
        finally
        {
            IsLoading = false;
            RecomputeStates();
        }
    }

    /// <summary>
    /// 接受 string 参数（不是 TopCategory enum）——AXAML 里 CommandParameter="Video" 是 string 字面量，
    /// 编译绑定无法自动转 enum，会让整个 View 静默渲染失败。这里手动 parse 一次。
    ///
    /// Toggle 语义：
    /// - "All"（或空字符串）→ 切到全部（null）
    /// - 与当前选中相同 → 取消选择，切到全部（null）
    /// - 其他 → 切到指定分类
    /// </summary>
    [RelayCommand]
    private Task SelectCategoryAsync(string? categoryName)
    {
        TopCategory? target;
        if (string.IsNullOrEmpty(categoryName) || categoryName.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            target = null;
        }
        else if (!Enum.TryParse<TopCategory>(categoryName, ignoreCase: true, out var cat))
        {
            Log.Warning("SelectCategoryAsync 收到非法 categoryName='{Name}'", categoryName);
            return Task.CompletedTask;
        }
        else
        {
            // Toggle：再次点击当前选中的分类 → 切回"全部"
            target = SelectedCategory == cat ? null : cat;
        }

        if (SelectedCategory == target) return Task.CompletedTask;
        SelectedCategory = target;
        PageNumber = 1;
        return LoadAsync();
    }

    /// <summary>
    /// 弹 MediaFilterDialog 让用户多维筛选；用户应用 → 把 Result 写到本 VM 的私有字段 + 重载第 1 页。
    /// 取消则不动当前筛选。
    /// </summary>
    [RelayCommand]
    private async Task OpenFilterAsync()
    {
        try
        {
            // 加载下拉用的全集（标签 + 收藏夹）。在同一线程同步——dialog 列表通常很短，无须 Task.Run。
            var allTags = await _tagService.GetAllTagsAsync();
            var allFavorites = await _favoriteService.GetAllFavoritesAsync();

            var result = await MediaFilterDialog.ShowAsync(
                currentTopCategory: SelectedCategory ?? TopCategory.Unknown,
                allTags: allTags,
                allFavorites: allFavorites,
                initialOnlyTop: _filterOnlyTopCategory,
                initialSelectedTopFilter: _filterTopCategoryOverride ?? TopCategory.Unknown,
                initialSelectedCategories: _filterCategories,
                initialSelectedTagName: _filterTagName,
                initialSelectedFavoriteName: _filterFavoriteName,
                initialMinRating: _filterMinRating,
                initialDateFilterType: _filterDateType?.ToString() ?? "ReleaseDate",
                initialStartDate: _filterStartDate,
                initialEndDate: _filterEndDate);

            if (result is null) return; // 取消

            _filterOnlyTopCategory = result.OnlyTopCategory;
            _filterTopCategoryOverride = result.SelectedTopCategoryFilter == TopCategory.Unknown
                ? null
                : result.SelectedTopCategoryFilter;
            _filterCategories = result.SelectedCategories.ToList();
            _filterTagName = result.SelectedTagName;
            _filterFavoriteName = result.SelectedFavoriteName;
            _filterMinRating = result.MinRating;
            _filterDateType = Enum.TryParse<DateFilterType>(result.DateFilterType, out var dt) ? dt : null;
            _filterStartDate = result.StartDate;
            _filterEndDate = result.EndDate;

            RefreshFilterSummary();
            PageNumber = 1;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenFilterAsync 失败");
        }
    }

    /// <summary>清除全部高级筛选条件（顶级 chip 不动，搜索框不动）。</summary>
    [RelayCommand]
    private async Task ClearFilterAsync()
    {
        if (!HasActiveFilter) return;

        _filterOnlyTopCategory = false;
        _filterTopCategoryOverride = null;
        _filterCategories.Clear();
        _filterTagName = null;
        _filterFavoriteName = null;
        _filterCircleId = null;
        _filterCircleName = null;
        _filterCreatorId = null;
        _filterCreatorName = null;
        _filterMinRating = null;
        _filterDateType = null;
        _filterStartDate = null;
        _filterEndDate = null;

        RefreshFilterSummary();
        PageNumber = 1;
        await LoadAsync();
    }

    /// <summary>"+ 新建（文件夹）"——弹原生 FolderPicker，把所选路径走 ManualAddMediaHelper</summary>
    [RelayCommand]
    private async Task NewMediaFromFolderAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            Log.Warning("NewMediaFromFolderAsync: 无法获取 StorageProvider");
            return;
        }

        try
        {
            var picked = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择要添加的文件夹",
                AllowMultiple = false,
            });
            var folder = picked.FirstOrDefault();
            var path = folder?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var newId = await ManualAddMediaHelper.OpenByPathAsync(path, _services);
            if (newId.HasValue)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NewMediaFromFolderAsync 失败");
        }
    }

    /// <summary>"+ 新建（文件）"——弹原生 FilePicker，单文件场景</summary>
    [RelayCommand]
    private async Task NewMediaFromFileAsync()
    {
        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            Log.Warning("NewMediaFromFileAsync: 无法获取 StorageProvider");
            return;
        }

        try
        {
            var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择要添加的媒体文件",
                AllowMultiple = false,
            });
            var file = picked.FirstOrDefault();
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var newId = await ManualAddMediaHelper.OpenByPathAsync(path, _services);
            if (newId.HasValue)
            {
                await LoadAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NewMediaFromFileAsync 失败");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private Task NextPageAsync()
    {
        PageNumber++;
        return LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private Task PrevPageAsync()
    {
        PageNumber--;
        return LoadAsync();
    }

    private bool CanGoNextPage() => PageNumber < TotalPages;
    private bool CanGoPrevPage() => PageNumber > 1;

    // SearchText / SelectedSort 改变时，回到第 1 页并重新加载
    partial void OnSearchTextChanged(string value) => DebouncedReload();
    partial void OnSelectedSortChanged(SortChoice value)
    {
        PageNumber = 1;
        _ = LoadAsync();
    }

    private CancellationTokenSource? _searchDebounceCts;
    private void DebouncedReload()
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    PageNumber = 1;
                    await LoadAsync();
                });
            }
            catch (TaskCanceledException) { /* 期望中：用户继续输入触发取消 */ }
        }, token);
    }

    private void RefreshFilterSummary()
    {
        var pieces = new List<string>();
        if (_filterOnlyTopCategory && _filterTopCategoryOverride.HasValue)
            pieces.Add("类型");
        if (_filterCategories.Count > 0)
            pieces.Add($"分类×{_filterCategories.Count}");
        if (!string.IsNullOrEmpty(_filterTagName))
            pieces.Add("标签");
        if (!string.IsNullOrEmpty(_filterFavoriteName))
            pieces.Add("收藏");
        if (_filterMinRating.HasValue)
            pieces.Add($"≥{_filterMinRating}星");
        if (_filterStartDate.HasValue || _filterEndDate.HasValue)
            pieces.Add("日期");

        HasActiveFilter = pieces.Count > 0;
        FilterSummary = pieces.Count == 0 ? "" : $"已筛选：{string.Join(" · ", pieces)}";
    }

    /// <summary>
    /// 取当前 Avalonia 主窗口对应的 TopLevel——picker / FAContentDialog 都需要它做 owner。
    /// 与 SourcesViewModel.GetTopLevel 复制；放在共享 Service 里更干净，但当前还没必要重构。
    /// </summary>
    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is not null)
        {
            return Avalonia.Controls.TopLevel.GetTopLevel(lifetime.MainWindow);
        }
        return null;
    }
}

public record SortChoice(MediaSortOption Option, string DisplayName);
