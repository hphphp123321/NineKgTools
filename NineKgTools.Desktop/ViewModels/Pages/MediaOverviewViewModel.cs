using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class MediaOverviewViewModel : PageViewModelBase
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCategoryVideo))]
    [NotifyPropertyChangedFor(nameof(IsCategoryAudio))]
    [NotifyPropertyChangedFor(nameof(IsCategoryGame))]
    [NotifyPropertyChangedFor(nameof(IsCategoryPicture))]
    [NotifyPropertyChangedFor(nameof(IsCategoryText))]
    private TopCategory _selectedCategory = TopCategory.Video;

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

    private void RecomputeStates()
    {
        HasItems = Items.Count > 0;
        ShowEmpty = !IsLoading && LoadError is null && Items.Count == 0;
        ShowError = !IsLoading && LoadError is not null;
    }

    // 给 AXAML 简化绑定用——避免在 XAML 写 enum 比较 converter
    public bool IsCategoryVideo => SelectedCategory == TopCategory.Video;
    public bool IsCategoryAudio => SelectedCategory == TopCategory.Audio;
    public bool IsCategoryGame => SelectedCategory == TopCategory.Game;
    public bool IsCategoryPicture => SelectedCategory == TopCategory.Picture;
    public bool IsCategoryText => SelectedCategory == TopCategory.Text;

    public MediaOverviewViewModel(MediaService mediaService, ImageCacheService imageCache)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
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
            var parameters = new MediaQueryParameters
            {
                TopCategory = SelectedCategory,
                FilterByTopCategoryOnly = true,
                Name = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
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
    /// </summary>
    [RelayCommand]
    private Task SelectCategoryAsync(string? categoryName)
    {
        if (string.IsNullOrEmpty(categoryName)) return Task.CompletedTask;
        if (!Enum.TryParse<TopCategory>(categoryName, ignoreCase: true, out var cat))
        {
            Log.Warning("SelectCategoryAsync 收到非法 categoryName='{Name}'", categoryName);
            return Task.CompletedTask;
        }
        if (SelectedCategory == cat) return Task.CompletedTask;
        SelectedCategory = cat;
        PageNumber = 1;
        return LoadAsync();
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
}

public record SortChoice(MediaSortOption Option, string DisplayName);
