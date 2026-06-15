using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Search;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.ViewModels;

/// <summary>
/// 全局搜索 Flyout——侧栏 SearchBox 聚焦时弹出的实时预览面板（参考 Web GlobalSearchBox）。
/// 输入 300ms 防抖 → 调 GlobalSearchService 拿 4 类型结果（每类 max 5 条） → 4 分组展示。
/// 支持键盘导航（↑↓/Enter/Esc/Ctrl+Enter）+ AI 语义开关（持久化到 DesktopPreferences）。
/// </summary>
public partial class GlobalSearchFlyoutViewModel : ObservableObject
{
    private readonly GlobalSearchService _searchService;
    private readonly NavigationService _nav;
    private readonly DesktopPreferences _preferences;
    private CancellationTokenSource? _searchCts;
    private DispatcherTimer? _debounceTimer;

    /// <summary>跨分组扁平化项列表——↑↓ 键盘导航在这上面跑，避免分组边界跳跃。</summary>
    private readonly List<FlyoutSearchItem> _flatItems = new();

    private const int MaxPerSection = 5;

    [ObservableProperty]
    private string _query = "";

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private long _elapsedMs;

    [ObservableProperty]
    private bool _enableVectorSearch;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ObservableCollection<FlyoutSearchItem> MediaItems { get; } = new();
    public ObservableCollection<FlyoutSearchItem> TagItems { get; } = new();
    public ObservableCollection<FlyoutSearchItem> CreatorItems { get; } = new();
    public ObservableCollection<FlyoutSearchItem> CircleItems { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMedia))]
    [NotifyPropertyChangedFor(nameof(HasAnyResults))]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _mediaCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTag))]
    [NotifyPropertyChangedFor(nameof(HasAnyResults))]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _tagCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCreator))]
    [NotifyPropertyChangedFor(nameof(HasAnyResults))]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _creatorCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCircle))]
    [NotifyPropertyChangedFor(nameof(HasAnyResults))]
    [NotifyPropertyChangedFor(nameof(TotalCount))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private int _circleCount;

    public bool HasMedia => MediaCount > 0;
    public bool HasTag => TagCount > 0;
    public bool HasCreator => CreatorCount > 0;
    public bool HasCircle => CircleCount > 0;
    public bool HasAnyResults => TotalCount > 0;
    public int TotalCount => MediaCount + TagCount + CreatorCount + CircleCount;

    public string SummaryText => HasAnyResults
        ? $"共 {TotalCount} 条 · {ElapsedMs}ms · Ctrl+Enter 查看全部"
        : "";

    public bool ShowEmptyState => !IsLoading && !HasAnyResults && !HasError;
    public string EmptyText => string.IsNullOrEmpty(Query)
        ? "🔍 开始探索——输入关键词搜索媒体、标签、创作者、社团"
        : $"未找到「{Query}」相关内容 · 试试更简短的关键词";

    public GlobalSearchFlyoutViewModel(GlobalSearchService searchService, NavigationService nav, DesktopPreferences preferences)
    {
        _searchService = searchService;
        _nav = nav;
        _preferences = preferences;
        _enableVectorSearch = preferences.EnableVectorSearch;
    }

    partial void OnQueryChanged(string value)
    {
        OnPropertyChanged(nameof(EmptyText));
        ScheduleSearch();
    }

    partial void OnEnableVectorSearchChanged(bool value)
    {
        if (_preferences.EnableVectorSearch != value)
        {
            _preferences.EnableVectorSearch = value;
            _preferences.RequestSave();
        }
        if (!string.IsNullOrWhiteSpace(Query)) ScheduleSearch();
    }

    private void ScheduleSearch()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            _debounceTimer?.Stop();
            _searchCts?.Cancel();
            ClearResults();
            return;
        }
        _debounceTimer ??= new DispatcherTimer(DispatcherPriority.Default) { Interval = TimeSpan.FromMilliseconds(300) };
        _debounceTimer.Stop();
        _debounceTimer.Tick -= OnDebounceTick;
        _debounceTimer.Tick += OnDebounceTick;
        _debounceTimer.Start();
    }

    private async void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounceTimer?.Stop();
        await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        var q = Query?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) { ClearResults(); return; }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var options = new GlobalSearchOptions
            {
                Query = q,
                EntityTypes = SearchEntityTypes.All,
                EnableVectorSearch = EnableVectorSearch,
            };
            var result = await _searchService.SearchAsync(options, token);
            if (token.IsCancellationRequested) return;

            ElapsedMs = result.ElapsedMilliseconds;
            ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage) ? null : result.ErrorMessage;
            ApplyResults(result);
        }
        catch (OperationCanceledException) { /* 用户继续打字，新 CTS 接力 */ }
        catch (Exception ex)
        {
            Log.Warning(ex, "GlobalSearchFlyout 搜索失败");
            ErrorMessage = "搜索失败，请稍后重试。";
            ClearResults();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyResults(GlobalSearchResult r)
    {
        MediaItems.Clear();
        foreach (var it in r.MediaResults.Take(MaxPerSection))
            MediaItems.Add(BuildMediaEntry(it));

        TagItems.Clear();
        foreach (var it in r.TagResults.Take(MaxPerSection))
            TagItems.Add(BuildTagEntry(it));

        CreatorItems.Clear();
        foreach (var it in r.CreatorResults.Take(MaxPerSection))
            CreatorItems.Add(BuildCreatorEntry(it));

        CircleItems.Clear();
        foreach (var it in r.CircleResults.Take(MaxPerSection))
            CircleItems.Add(BuildCircleEntry(it));

        MediaCount = MediaItems.Count;
        TagCount = TagItems.Count;
        CreatorCount = CreatorItems.Count;
        CircleCount = CircleItems.Count;

        RebuildFlatItems();
        // 不默认高亮——用户按 ↓ 才进入键盘导航模式（Web search dropdown 标准体验）
    }

    private void ClearResults()
    {
        MediaItems.Clear();
        TagItems.Clear();
        CreatorItems.Clear();
        CircleItems.Clear();
        MediaCount = 0;
        TagCount = 0;
        CreatorCount = 0;
        CircleCount = 0;
        _flatItems.Clear();
        ElapsedMs = 0;
    }

    private void RebuildFlatItems()
    {
        _flatItems.Clear();
        foreach (var x in MediaItems) _flatItems.Add(x);
        foreach (var x in TagItems) _flatItems.Add(x);
        foreach (var x in CreatorItems) _flatItems.Add(x);
        foreach (var x in CircleItems) _flatItems.Add(x);
    }

    private void SetHighlightedIndex(int index)
    {
        if (_flatItems.Count == 0) return;
        index = Math.Clamp(index, 0, _flatItems.Count - 1);
        for (int i = 0; i < _flatItems.Count; i++)
            _flatItems[i].IsHighlighted = (i == index);
    }

    private int CurrentHighlightedIndex => _flatItems.FindIndex(x => x.IsHighlighted);

    /// <summary>是否有用户主动选中的高亮项（用户至少按过一次 ↓/↑）。
    /// MainWindow 的 Enter 路由用：无高亮 → 跳完整页；有高亮 → 激活该项。</summary>
    public bool HasKeyboardSelection => CurrentHighlightedIndex >= 0;

    /// <summary>↑↓ 键盘导航——跨分组无缝跳。</summary>
    public void MoveSelection(int delta)
    {
        if (_flatItems.Count == 0) return;
        var idx = CurrentHighlightedIndex;
        if (idx < 0) idx = 0;
        else idx = (idx + delta + _flatItems.Count) % _flatItems.Count;
        SetHighlightedIndex(idx);
    }

    /// <summary>Enter——激活当前高亮项，跳对应详情 / 列表页。</summary>
    public async Task ActivateHighlightedAsync()
    {
        var idx = CurrentHighlightedIndex;
        if (idx < 0 || idx >= _flatItems.Count) return;
        await ActivateEntryAsync(_flatItems[idx]);
    }

    /// <summary>点击单条结果——与键盘 Enter 同语义。</summary>
    [RelayCommand]
    public async Task ActivateEntryAsync(FlyoutSearchItem? item)
    {
        if (item is null) return;
        IsOpen = false;
        try
        {
            switch (item.Kind)
            {
                case FlyoutEntryKind.Media:
                    await _nav.NavigateToAsync<MediaDetailViewModel>(vm =>
                    {
                        vm.Mode = MediaDetailMode.EmbeddedPage;
                        vm.RequestOpenDetail(item.Id);
                    });
                    break;
                case FlyoutEntryKind.Tag:
                    await _nav.NavigateToAsync<TagsViewModel>(vm => vm.RequestOpenDetail(item.Id));
                    break;
                case FlyoutEntryKind.Creator:
                    await _nav.NavigateToAsync<CreatorsViewModel>(vm => vm.RequestOpenDetail(item.Id));
                    break;
                case FlyoutEntryKind.Circle:
                    await _nav.NavigateToAsync<CirclesViewModel>(vm => vm.RequestOpenDetail(item.Id));
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GlobalSearchFlyout 激活条目失败：{Kind} Id={Id}", item.Kind, item.Id);
        }
    }

    /// <summary>Ctrl+Enter / footer "查看全部"——跳到 SearchResultPage 完整结果页，复用 query + AI 状态。</summary>
    [RelayCommand]
    public async Task ViewAllAsync()
    {
        var q = Query?.Trim() ?? "";
        if (string.IsNullOrEmpty(q)) return;
        IsOpen = false;
        await _nav.NavigateToAsync<SearchResultViewModel>(vm =>
        {
            vm.Query = q;
            vm.EnableVectorSearch = EnableVectorSearch;
        });
    }

    private static FlyoutSearchItem BuildMediaEntry(SearchResultItem<MediaBase> it)
    {
        var subtitle = new List<string>();
        if (!string.IsNullOrWhiteSpace(it.Entity.Circle?.Name)) subtitle.Add(it.Entity.Circle.Name);
        subtitle.Add(it.Entity.Category?.Name ?? "未分类");
        return new FlyoutSearchItem
        {
            Kind = FlyoutEntryKind.Media,
            Id = it.Entity.Id,
            Title = it.Entity.Title ?? "(无标题)",
            Subtitle = string.Join(" · ", subtitle),
            MatchTypeText = MapMatchType(it.MatchType),
            RelevancePercent = $"{(int)Math.Round(it.RelevanceScore * 100)}%",
            CategoryKind = it.Entity.Category?.TopCategory ?? TopCategory.Unknown,
        };
    }

    private static FlyoutSearchItem BuildTagEntry(SearchResultItem<Core.Models.Tags.Tag> it)
    {
        // 与 TagItemViewModel 一致：TopTag.Id % 5 映射 5 类别色
        IBrush? accent = null;
        IBrush? fill = null;
        if (it.Entity.TopTag is not null)
        {
            var idx = ((it.Entity.TopTag.Id % 5) + 5) % 5;
            accent = ResolveBrush(TagAccentKeys[idx]);
            fill = ResolveBrush(TagFillKeys[idx]);
        }
        return new FlyoutSearchItem
        {
            Kind = FlyoutEntryKind.Tag,
            Id = it.Entity.Id,
            Title = it.Entity.Name ?? "(无名)",
            Subtitle = it.Entity.TopTag?.Name ?? it.MatchDetails,
            MatchTypeText = MapMatchType(it.MatchType),
            RelevancePercent = $"{(int)Math.Round(it.RelevanceScore * 100)}%",
            // chip 专用非空 brush——XAML 不再用非法的 FallbackValue={DynamicResource}
            ChipFillBrush = fill ?? ResolveBrush("SubtleFillColorTertiaryBrush"),
            ChipBorderBrush = accent ?? ResolveBrush("ControlStrokeColorDefaultBrush"),
            ChipGlyphBrush = accent ?? ResolveBrush("TextFillColorPrimaryBrush"),
        };
    }

    private static readonly string[] TagAccentKeys =
    {
        "BrandCategoryVideoBrush",
        "BrandCategoryAudioBrush",
        "BrandCategoryGameBrush",
        "BrandCategoryPictureBrush",
        "BrandCategoryTextBrush",
    };

    private static readonly string[] TagFillKeys =
    {
        "BrandCategoryVideoFillBrush",
        "BrandCategoryAudioFillBrush",
        "BrandCategoryGameFillBrush",
        "BrandCategoryPictureFillBrush",
        "BrandCategoryTextFillBrush",
    };

    // ResourceLookup 沿 Styles 链搜索，FluentAvalonia 主题 brush 才命中（见 CLAUDE.md §4.8）
    private static IBrush? ResolveBrush(string key) => ResourceLookup.Brush(key);

    private static FlyoutSearchItem BuildCreatorEntry(SearchResultItem<Core.Models.Media.Creator> it)
    {
        return new FlyoutSearchItem
        {
            Kind = FlyoutEntryKind.Creator,
            Id = it.Entity.Id,
            Title = it.Entity.Name ?? "(无名)",
            Subtitle = it.MatchDetails,
            MatchTypeText = MapMatchType(it.MatchType),
            RelevancePercent = $"{(int)Math.Round(it.RelevanceScore * 100)}%",
        };
    }

    private static FlyoutSearchItem BuildCircleEntry(SearchResultItem<Core.Models.Media.Circle> it)
    {
        return new FlyoutSearchItem
        {
            Kind = FlyoutEntryKind.Circle,
            Id = it.Entity.Id,
            Title = it.Entity.Name ?? "(无名)",
            Subtitle = it.MatchDetails,
            MatchTypeText = MapMatchType(it.MatchType),
            RelevancePercent = $"{(int)Math.Round(it.RelevanceScore * 100)}%",
        };
    }

    private static string MapMatchType(SearchMatchType t) => t switch
    {
        SearchMatchType.Exact => "精确",
        SearchMatchType.Fuzzy => "模糊",
        SearchMatchType.Contains => "包含",
        SearchMatchType.Vector => "语义",
        SearchMatchType.Alias => "别名",
        SearchMatchType.Description => "描述",
        _ => t.ToString(),
    };
}

public enum FlyoutEntryKind { Media, Tag, Creator, Circle }

/// <summary>Flyout 列表的扁平条目——4 类型共用，按 Kind 渲染不同图标 / 跳转目标。</summary>
public partial class FlyoutSearchItem : ObservableObject
{
    public FlyoutEntryKind Kind { get; init; }
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    public string MatchTypeText { get; init; } = "";
    public string RelevancePercent { get; init; } = "";
    /// <summary>媒体特有：用于渲染类别图标颜色（其他类型为 Unknown）</summary>
    public TopCategory CategoryKind { get; init; } = TopCategory.Unknown;
    // 标签 chip 专用非空 brush（与 TagItemViewModel 同算法 TopTag.Id % 5；孤儿标签退化中性）。
    // 非空 → XAML 直接绑定，不用非法的 {Binding ..., FallbackValue={DynamicResource}}。
    /// <summary>chip 背景填充：有 TopTag 色用色系 fill，否则中性 Subtle。</summary>
    public IBrush? ChipFillBrush { get; init; }
    /// <summary>chip 边框：有 TopTag 色用 accent，否则中性 ControlStroke。</summary>
    public IBrush? ChipBorderBrush { get; init; }
    /// <summary>chip "#" 字形：有色用 accent，否则正文主色 TextPrimary。</summary>
    public IBrush? ChipGlyphBrush { get; init; }

    /// <summary>键盘导航 / 鼠标 hover 的高亮态——driven by GlobalSearchFlyoutViewModel.SetHighlightedIndex。</summary>
    [ObservableProperty]
    private bool _isHighlighted;
}
