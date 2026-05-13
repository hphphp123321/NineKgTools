using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Search;
using NineKgTools.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 全局搜索结果页（§13 第四波尾巴 + §8.1 P0/P1）。
/// 调用 GlobalSearchService.SearchAsync 拿 4 类型结果，分 Tab 展示：媒体 / 标签 / 创作者 / 社团。
/// 媒体结果点击进 MediaDetailWindow；其他类型仅展示（链跳转留 P3）。
/// </summary>
public partial class SearchResultViewModel : PageViewModelBase
{
    private readonly GlobalSearchService _searchService;
    private readonly ImageCacheService _imageCache;
    private CancellationTokenSource? _searchCts;

    public override string Title => string.IsNullOrEmpty(Query) ? "搜索" : $"搜索: {Query}";

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private bool _enableVectorSearch;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private long _elapsedMs;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasSearched;

    /// <summary>0=媒体 / 1=标签 / 2=创作者 / 3=社团</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _mediaItems = new();

    [ObservableProperty]
    private ObservableCollection<TagItemViewModel> _tagItems = new();

    [ObservableProperty]
    private ObservableCollection<CreatorItemViewModel> _creatorItems = new();

    [ObservableProperty]
    private ObservableCollection<CircleItemViewModel> _circleItems = new();

    [ObservableProperty]
    private int _mediaCount;

    [ObservableProperty]
    private int _tagCount;

    [ObservableProperty]
    private int _creatorCount;

    [ObservableProperty]
    private int _circleCount;

    public bool HasAnyResults => MediaCount + TagCount + CreatorCount + CircleCount > 0;
    public string SummaryText => HasSearched
        ? (HasAnyResults
            ? $"共 {MediaCount + TagCount + CreatorCount + CircleCount} 条结果 · {ElapsedMs}ms"
            : $"未找到匹配 · {ElapsedMs}ms")
        : "";

    public SearchResultViewModel(GlobalSearchService searchService, ImageCacheService imageCache)
    {
        _searchService = searchService;
        _imageCache = imageCache;
    }

    public override async Task OnEnterAsync()
    {
        // 如果通过 ExecuteSearch 跳过来时 Query 已设——立即触发搜索
        if (!string.IsNullOrWhiteSpace(Query))
        {
            await ExecuteSearchAsync();
        }
    }

    public override Task OnLeaveAsync()
    {
        _searchCts?.Cancel();
        _searchCts = null;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ExecuteSearchAsync()
    {
        var q = Query?.Trim() ?? "";
        if (string.IsNullOrEmpty(q))
        {
            ErrorMessage = "搜索关键词不能为空";
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        HasSearched = true;

        try
        {
            var options = new GlobalSearchOptions
            {
                Query = q,
                EntityTypes = SearchEntityTypes.All,
                EnableVectorSearch = EnableVectorSearch,
            };

            var result = await _searchService.SearchAsync(options, token);

            ElapsedMs = result.ElapsedMilliseconds;
            ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage) ? null : result.ErrorMessage;

            // 按类型映射到对应 ItemViewModel
            MediaItems = new ObservableCollection<MediaCardViewModel>(
                result.MediaResults.Select(r => new MediaCardViewModel(r.Entity, _imageCache)));
            TagItems = new ObservableCollection<TagItemViewModel>(
                result.TagResults.Select(r => new TagItemViewModel(r.Entity)));
            CreatorItems = new ObservableCollection<CreatorItemViewModel>(
                result.CreatorResults.Select(r => new CreatorItemViewModel(r.Entity, _imageCache)));
            CircleItems = new ObservableCollection<CircleItemViewModel>(
                result.CircleResults.Select(r => new CircleItemViewModel(r.Entity, _imageCache)));

            MediaCount = MediaItems.Count;
            TagCount = TagItems.Count;
            CreatorCount = CreatorItems.Count;
            CircleCount = CircleItems.Count;

            OnPropertyChanged(nameof(HasAnyResults));
            OnPropertyChanged(nameof(SummaryText));

            // 自动切到第一个有结果的 Tab
            if (MediaCount > 0) SelectedTabIndex = 0;
            else if (TagCount > 0) SelectedTabIndex = 1;
            else if (CreatorCount > 0) SelectedTabIndex = 2;
            else if (CircleCount > 0) SelectedTabIndex = 3;
        }
        catch (OperationCanceledException)
        {
            Log.Information("SearchResult 搜索已取消：{Query}", q);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SearchResult 搜索失败：{Query}", q);
            ErrorMessage = "搜索失败，请稍后重试。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>跳到标签详情——通过 NavigationService 切到 TagsViewModel + PendingIntent</summary>
    [RelayCommand]
    private async Task OpenTagAsync(int id)
    {
        try
        {
            var nav = Program.Services?.GetService<NavigationService>();
            if (nav is null) return;
            await nav.NavigateToAsync<TagsViewModel>(vm => vm.RequestOpenDetail(id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SearchResult 跳标签详情失败 Id={Id}", id);
        }
    }

    [RelayCommand]
    private async Task OpenCreatorAsync(int id)
    {
        try
        {
            var nav = Program.Services?.GetService<NavigationService>();
            if (nav is null) return;
            await nav.NavigateToAsync<CreatorsViewModel>(vm => vm.RequestOpenDetail(id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SearchResult 跳创作者详情失败 Id={Id}", id);
        }
    }

    [RelayCommand]
    private async Task OpenCircleAsync(int id)
    {
        try
        {
            var nav = Program.Services?.GetService<NavigationService>();
            if (nav is null) return;
            await nav.NavigateToAsync<CirclesViewModel>(vm => vm.RequestOpenDetail(id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SearchResult 跳社团详情失败 Id={Id}", id);
        }
    }
}
