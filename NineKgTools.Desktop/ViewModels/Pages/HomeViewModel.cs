using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly Config _config;
    private readonly MonitorService _monitorService;
    private readonly TaskProgressService _progressService;
    private readonly NavigationService _navigation;
    private readonly ImageCacheService _imageCache;
    private DispatcherTimer? _refreshTimer;
    private DispatcherTimer? _countUpTimer;

    public override string Title => "首页";

    /// <summary>5 类别 stacked horizontal bar 的固定总宽度。
    /// 首页 hero 卡右栏宽度有限，固定 480px 比绑容器宽度（需要 layout updated callback）简单稳定。</summary>
    private const double CategoryBarTotalWidth = 480;

    // ========== 实体计数（OnEnter 刷一次） ==========

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMedia))]
    [NotifyPropertyChangedFor(nameof(EmptyHint))]
    private int _mediaCount;

    /// <summary>Hero 区显示的数字 —— count-up 动画驱动；动画完成 = MediaCount。
    /// OnEnter 拉到真实 MediaCount 后用 DispatcherTimer 700ms 缓动到 final。</summary>
    [ObservableProperty]
    private int _displayMediaCount;

    [ObservableProperty]
    private int _creatorCount;

    [ObservableProperty]
    private int _circleCount;

    [ObservableProperty]
    private int _tagCount;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private string _totalSizeText = "—";

    // ========== 5 类型分类计数 ==========

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideoBarWidth))]
    [NotifyPropertyChangedFor(nameof(HasVideoBar))]
    private int _videoCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AudioBarWidth))]
    [NotifyPropertyChangedFor(nameof(HasAudioBar))]
    private int _audioCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameBarWidth))]
    [NotifyPropertyChangedFor(nameof(HasGameBar))]
    private int _gameCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PictureBarWidth))]
    [NotifyPropertyChangedFor(nameof(HasPictureBar))]
    private int _pictureCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextBarWidth))]
    [NotifyPropertyChangedFor(nameof(HasTextBar))]
    private int _textCount;

    // ========== 实时状态（5s 轮询） ==========

    [ObservableProperty]
    private int _watchFolderTotal;

    [ObservableProperty]
    private int _watchFolderActive;

    [ObservableProperty]
    private int _runningTaskCount;

    [ObservableProperty]
    private int _failedTaskCount;

    // ========== 待处理快速入口 ==========

    [ObservableProperty]
    private int _pendingIdentifyCount;

    [ObservableProperty]
    private int _pendingDatabaseCount;

    // ========== 库的丰富度展示（Recent / TopTags / TopActiveCreators） ==========

    /// <summary>最近添加的 8 部媒体（DB 上限）——OnEnter 时按 StoreDate desc 拉，作为"库的温度"+ 跳详情入口</summary>
    public ObservableCollection<RecentMediaItemVm> RecentMedias { get; } = new();
    public bool HasRecentMedias => RecentMedias.Count > 0;

    /// <summary>UI 实际可见的最近添加数量——由 HomePage.axaml.cs SizeChanged 监听 RecentSection 容器宽度算出
    /// （responsive overflow=clip：能放几张显示几张，多了不显示，绝不横滚）。
    /// 默认 8，code-behind 会立刻基于实际宽度修正。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleRecentMedias))]
    private int _visibleRecentCount = 8;

    /// <summary>UI 绑定的视图——RecentMedias 前 VisibleRecentCount 项。
    /// VisibleRecentCount 变化时 raise，ItemsControl 自动刷子项。</summary>
    public IEnumerable<RecentMediaItemVm> VisibleRecentMedias => RecentMedias.Take(VisibleRecentCount);

    /// <summary>Top 12 标签——按关联 media count desc，权重 sqrt 映射 12-22px 字号</summary>
    public ObservableCollection<TopTagItemVm> TopTags { get; } = new();
    public bool HasTopTags => TopTags.Count > 0;

    /// <summary>Top 5 活跃创作者——按关联 media count desc</summary>
    public ObservableCollection<TopCreatorItemVm> TopActiveCreators { get; } = new();
    public bool HasTopCreators => TopActiveCreators.Count > 0;

    // ========== 派生属性 ==========

    /// <summary>是否已有媒体——决定首页走"富信息"还是"空态引导"两个 root 分支。</summary>
    public bool HasMedia => MediaCount > 0;

    /// <summary>按当前小时返回中文时段问候。早 5-11 / 中 11-13 / 下午 13-18 / 晚 18-5。</summary>
    public string Greeting
    {
        get
        {
            var h = DateTime.Now.Hour;
            return h switch
            {
                >= 5 and < 11 => "早上好",
                >= 11 and < 13 => "中午好",
                >= 13 and < 18 => "下午好",
                _ => "晚上好",
            };
        }
    }

    /// <summary>空态副标题——MediaCount == 0 时的引导文案</summary>
    public string EmptyHint => HasMedia
        ? ""
        : "添加一个监视文件夹或单个文件，NineKgTools 会自动识别并整理它们。";

    public string WatchFolderText => WatchFolderTotal == 0
        ? "未配置"
        : $"{WatchFolderActive} / {WatchFolderTotal} 监控中";

    public string RunningTaskText => RunningTaskCount == 0 ? "无运行中任务" : $"{RunningTaskCount} 个运行中";
    public string FailedTaskText => FailedTaskCount == 0 ? "无失败任务" : $"{FailedTaskCount} 个失败需关注";
    public bool HasFailedTasks => FailedTaskCount > 0;
    public bool HasPending => PendingIdentifyCount + PendingDatabaseCount > 0;

    public string PendingActionText
    {
        get
        {
            if (!HasPending) return "无待处理项";
            if (PendingIdentifyCount > 0 && PendingDatabaseCount > 0)
                return $"{PendingIdentifyCount} 待识别 · {PendingDatabaseCount} 待入库";
            if (PendingIdentifyCount > 0) return $"{PendingIdentifyCount} 项待识别";
            return $"{PendingDatabaseCount} 项待入库";
        }
    }

    // ========== mini stacked bar 宽度派生 ==========

    public double VideoBarWidth => ComputeBarWidth(VideoCount);
    public double AudioBarWidth => ComputeBarWidth(AudioCount);
    public double GameBarWidth => ComputeBarWidth(GameCount);
    public double PictureBarWidth => ComputeBarWidth(PictureCount);
    public double TextBarWidth => ComputeBarWidth(TextCount);

    public bool HasVideoBar => VideoCount > 0;
    public bool HasAudioBar => AudioCount > 0;
    public bool HasGameBar => GameCount > 0;
    public bool HasPictureBar => PictureCount > 0;
    public bool HasTextBar => TextCount > 0;

    private double ComputeBarWidth(int categoryCount)
    {
        if (categoryCount <= 0 || MediaCount <= 0) return 0;
        var w = CategoryBarTotalWidth * categoryCount / (double)MediaCount;
        return Math.Max(w, 6);
    }

    public HomeViewModel(IDbContextFactory<MediaDbContext> dbFactory,
        Config config, MonitorService monitorService,
        TaskProgressService progressService,
        NavigationService navigation,
        ImageCacheService imageCache)
    {
        _dbFactory = dbFactory;
        _config = config;
        _monitorService = monitorService;
        _progressService = progressService;
        _navigation = navigation;
        _imageCache = imageCache;
    }

    public override async Task OnEnterAsync()
    {
        OnPropertyChanged(nameof(Greeting));

        await RefreshEntityCountsAsync();
        await RefreshLibraryRichnessAsync();
        RefreshDesktopStats();

        // Hero 数字 count-up 入场动画（700ms ease-out-quart）。仅 HasMedia 时启动
        if (HasMedia) StartCountUpAnimation();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => RefreshDesktopStats();
        _refreshTimer.Start();
    }

    public override Task OnLeaveAsync()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        _countUpTimer?.Stop();
        _countUpTimer = null;
        return Task.CompletedTask;
    }

    /// <summary>Hero 数字 count-up 动画——700ms cubic ease-out，60fps tick。
    /// MediaCount 较小（&lt; 50）时直接显示 final 避免视觉跳跃过快。</summary>
    private void StartCountUpAnimation()
    {
        _countUpTimer?.Stop();
        var target = MediaCount;
        if (target <= 0)
        {
            DisplayMediaCount = 0;
            return;
        }
        // 太小的库直接显示，避免数字翻几下就到的廉价感
        if (target < 30)
        {
            DisplayMediaCount = target;
            return;
        }

        var start = DateTime.UtcNow;
        var duration = TimeSpan.FromMilliseconds(700);
        DisplayMediaCount = 0;
        _countUpTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _countUpTimer.Tick += (_, _) =>
        {
            var elapsed = DateTime.UtcNow - start;
            if (elapsed >= duration)
            {
                DisplayMediaCount = target;
                _countUpTimer?.Stop();
                _countUpTimer = null;
                return;
            }
            // ease-out-quart: 1 - (1-t)^4
            var t = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
            var eased = 1 - Math.Pow(1 - t, 4);
            DisplayMediaCount = (int)(target * eased);
        };
        _countUpTimer.Start();
    }

    private async Task RefreshEntityCountsAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            MediaCount = await db.Medias.CountAsync();
            CreatorCount = await db.Creators.CountAsync();
            CircleCount = await db.Circles.CountAsync();
            TagCount = await db.Tags.CountAsync();
            FavoriteCount = await db.Favorites.CountAsync();

            var groups = await db.Medias
                .AsNoTracking()
                .Include(m => m.Category)
                .GroupBy(m => m.Category.TopCategory)
                .Select(g => new { Top = g.Key, Count = g.Count() })
                .ToListAsync();
            VideoCount = groups.FirstOrDefault(g => g.Top == TopCategory.Video)?.Count ?? 0;
            AudioCount = groups.FirstOrDefault(g => g.Top == TopCategory.Audio)?.Count ?? 0;
            GameCount = groups.FirstOrDefault(g => g.Top == TopCategory.Game)?.Count ?? 0;
            PictureCount = groups.FirstOrDefault(g => g.Top == TopCategory.Picture)?.Count ?? 0;
            TextCount = groups.FirstOrDefault(g => g.Top == TopCategory.Text)?.Count ?? 0;

            long totalBytes = await db.Medias.AsNoTracking().SumAsync(m => (long?)m.Size) ?? 0;
            TotalSizeText = FormatBytes(totalBytes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HomeViewModel 加载实体计数失败");
        }
    }

    /// <summary>拉首页"丰富度"展示数据 —— 最近 8 部 / Top 12 标签 / Top 5 创作者。
    /// 三个查询并行，单独 try 避免一个失败影响其他。</summary>
    private async Task RefreshLibraryRichnessAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            // 1) 最近添加 8 部：优先 StoreDate desc，null 时 fallback Id desc
            var recent = await db.Medias
                .AsNoTracking()
                .Include(m => m.Category)
                .Include(m => m.Pictures)
                .OrderByDescending(m => m.StoreDate ?? DateTime.MinValue)
                .ThenByDescending(m => m.Id)
                .Take(8)
                .ToListAsync();
            RecentMedias.Clear();
            foreach (var m in recent)
            {
                var poster = m.Pictures?.FirstOrDefault()?.Name;
                var vm = new RecentMediaItemVm(
                    m.Id,
                    m.Title,
                    m.Category?.TopCategory ?? TopCategory.Unknown,
                    _navigation,
                    _imageCache,
                    poster);
                RecentMedias.Add(vm);
            }
            OnPropertyChanged(nameof(HasRecentMedias));
            OnPropertyChanged(nameof(VisibleRecentMedias));

            // 2) Top 12 标签：按关联 media 数 desc
            var tagsRaw = await db.Tags
                .AsNoTracking()
                .Where(t => t.Medias.Count > 0)
                .OrderByDescending(t => t.Medias.Count)
                .Take(12)
                .Select(t => new { t.Id, t.Name, Count = t.Medias.Count })
                .ToListAsync();
            TopTags.Clear();
            if (tagsRaw.Count > 0)
            {
                var maxCount = tagsRaw[0].Count;
                var minCount = tagsRaw[^1].Count;
                foreach (var t in tagsRaw)
                {
                    // sqrt 映射 12-22px：log/sqrt 比 linear 更平衡，避免头部 tag 一家独大
                    var norm = maxCount == minCount
                        ? 0.7
                        : Math.Sqrt((t.Count - minCount) / (double)(maxCount - minCount));
                    var fontSize = 12 + norm * 10; // 12-22
                    TopTags.Add(new TopTagItemVm(t.Id, t.Name, t.Count, fontSize, _navigation));
                }
            }
            OnPropertyChanged(nameof(HasTopTags));

            // 3) Top 5 活跃创作者
            var creatorsRaw = await db.Creators
                .AsNoTracking()
                .Where(c => c.Medias.Count > 0)
                .OrderByDescending(c => c.Medias.Count)
                .Take(5)
                .Select(c => new { c.Id, c.Name, Count = c.Medias.Count })
                .ToListAsync();
            TopActiveCreators.Clear();
            foreach (var c in creatorsRaw)
            {
                TopActiveCreators.Add(new TopCreatorItemVm(c.Id, c.Name, c.Count, _navigation));
            }
            OnPropertyChanged(nameof(HasTopCreators));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HomeViewModel.RefreshLibraryRichnessAsync 失败（非阻塞）");
        }
    }

    private void RefreshDesktopStats()
    {
        try
        {
            var configured = _config.Source?.WatchFolders ?? new List<string>();
            WatchFolderTotal = configured.Count;
            WatchFolderActive = configured.Count(p => _monitorService.IsMonitoring(p));

            var allTasks = _progressService.GetAllRootTasks().ToList();
            RunningTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying);
            FailedTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Failed
                or TaskExecutionStatus.Timeout);

            _ = RefreshPendingCountsAsync();

            OnPropertyChanged(nameof(WatchFolderText));
            OnPropertyChanged(nameof(RunningTaskText));
            OnPropertyChanged(nameof(FailedTaskText));
            OnPropertyChanged(nameof(HasFailedTasks));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HomeViewModel.RefreshDesktopStats 失败");
        }
    }

    private async Task RefreshPendingCountsAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            PendingIdentifyCount = await db.MediaSources
                .AsNoTracking()
                .CountAsync(s => !s.Identified);
            PendingDatabaseCount = await db.MediaSources
                .AsNoTracking()
                .CountAsync(s => s.Identified && !s.InDatabase);

            OnPropertyChanged(nameof(HasPending));
            OnPropertyChanged(nameof(PendingActionText));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "RefreshPendingCountsAsync 失败（非阻塞）");
        }
    }

    // ========== 快速跳转命令 ==========

    [RelayCommand]
    private Task GoToMediaLibrary() => _navigation.NavigateToAsync<MediaOverviewViewModel>();

    [RelayCommand]
    private Task GoToCreators() => _navigation.NavigateToAsync<CreatorsViewModel>();

    [RelayCommand]
    private Task GoToCircles() => _navigation.NavigateToAsync<CirclesViewModel>();

    [RelayCommand]
    private Task GoToTags() => _navigation.NavigateToAsync<TagsViewModel>();

    [RelayCommand]
    private Task GoToFavorites() => _navigation.NavigateToAsync<FavoritesViewModel>();

    [RelayCommand]
    private Task GoToTasks() => _navigation.NavigateToAsync<BackgroundTasksViewModel>();

    [RelayCommand]
    private Task GoToPending() => _navigation.NavigateToAsync<PendingMediaViewModel>();

    /// <summary>空态主 CTA 跳转：媒体源页是"添加媒体"的真实落点</summary>
    [RelayCommand]
    private Task GoToSources() => _navigation.NavigateToAsync<SourcesViewModel>();

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        return bytes switch
        {
            >= 1L << 40 => $"{bytes / (double)(1L << 40):F2} TB",
            >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
            >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
            >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
            _ => $"{bytes} B",
        };
    }
}

// ========== Row-level VM（首页内嵌；不抽到独立文件因为只在首页用） ==========

/// <summary>最近添加 mini poster 卡的 VM</summary>
public partial class RecentMediaItemVm : ObservableObject
{
    private readonly NavigationService _navigation;
    private readonly ImageCacheService _imageCache;

    public int Id { get; }
    public string Title { get; }
    public TopCategory TopCategory { get; }

    [ObservableProperty]
    private Bitmap? _cover;

    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);

    public RecentMediaItemVm(int id, string title, TopCategory topCategory,
        NavigationService navigation, ImageCacheService imageCache, string? posterName)
    {
        Id = id;
        Title = title;
        TopCategory = topCategory;
        _navigation = navigation;
        _imageCache = imageCache;
        if (!string.IsNullOrEmpty(posterName)) _ = LoadCoverAsync(posterName);
    }

    private async Task LoadCoverAsync(string posterName)
    {
        try
        {
            Cover = await _imageCache.GetOrLoadAsync(posterName);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "RecentMediaItemVm 加载封面失败 Id={Id}", Id);
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        try
        {
            await _navigation.NavigateToAsync<MediaDetailViewModel>(vm => vm.RequestOpenDetail(Id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "RecentMediaItemVm 打开媒体失败 Id={Id}", Id);
        }
    }
}

/// <summary>Top 标签云每项的 VM——FontSize 由 HomeViewModel 按 sqrt 权重算好传入</summary>
public partial class TopTagItemVm : ObservableObject
{
    private readonly NavigationService _navigation;

    public int Id { get; }
    public string Name { get; }
    public int Count { get; }
    public double FontSize { get; }

    public TopTagItemVm(int id, string name, int count, double fontSize, NavigationService navigation)
    {
        Id = id;
        Name = name;
        Count = count;
        FontSize = fontSize;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        try
        {
            await _navigation.NavigateToAsync<TagsViewModel>(vm => vm.RequestOpenDetail(Id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TopTagItemVm 打开标签失败 Id={Id}", Id);
        }
    }
}

/// <summary>Top 活跃创作者 list 每项的 VM</summary>
public partial class TopCreatorItemVm : ObservableObject
{
    private readonly NavigationService _navigation;

    public int Id { get; }
    public string Name { get; }
    public int Count { get; }
    public string CountText => $"{Count} 部";

    public TopCreatorItemVm(int id, string name, int count, NavigationService navigation)
    {
        Id = id;
        Name = name;
        Count = count;
        _navigation = navigation;
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        try
        {
            await _navigation.NavigateToAsync<CreatorsViewModel>(vm => vm.RequestOpenDetail(Id));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TopCreatorItemVm 打开创作者失败 Id={Id}", Id);
        }
    }
}
