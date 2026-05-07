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
    private DispatcherTimer? _refreshTimer;

    public override string Title => "首页";

    // ========== 实体计数（OnEnter 刷一次，不进入轮询） ==========

    [ObservableProperty]
    private int _mediaCount;

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

    // ========== 5 类型分类计数（媒体类型预览卡片底部显示） ==========

    [ObservableProperty]
    private int _videoCount;

    [ObservableProperty]
    private int _audioCount;

    [ObservableProperty]
    private int _gameCount;

    [ObservableProperty]
    private int _pictureCount;

    [ObservableProperty]
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

    public string WatchFolderText => WatchFolderTotal == 0
        ? "未配置"
        : $"{WatchFolderActive} / {WatchFolderTotal} 监控中";

    public string RunningTaskText => RunningTaskCount == 0 ? "无运行中任务" : $"{RunningTaskCount} 个运行中";
    public string FailedTaskText => FailedTaskCount == 0 ? "暂无失败" : $"{FailedTaskCount} 个失败需关注";
    public bool HasFailedTasks => FailedTaskCount > 0;
    public bool HasPending => PendingIdentifyCount + PendingDatabaseCount > 0;
    public string PendingText => HasPending
        ? $"待识别 {PendingIdentifyCount} · 待入库 {PendingDatabaseCount}"
        : "无待处理";

    public HomeViewModel(IDbContextFactory<MediaDbContext> dbFactory,
        Config config, MonitorService monitorService,
        TaskProgressService progressService,
        NavigationService navigation)
    {
        _dbFactory = dbFactory;
        _config = config;
        _monitorService = monitorService;
        _progressService = progressService;
        _navigation = navigation;
    }

    public override async Task OnEnterAsync()
    {
        await RefreshEntityCountsAsync();
        RefreshDesktopStats();

        // 5s 轮询桌面端实时状态（监控状态 + 任务计数 + 待处理计数）
        // 实体计数（媒体/创作者/标签/...）变化频率低，进入页面拉一次即可；OnEnter 重新拉
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => RefreshDesktopStats();
        _refreshTimer.Start();
    }

    public override Task OnLeaveAsync()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        return Task.CompletedTask;
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

            // 5 顶级分类计数（按 Category.TopCategory group）
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

            // 占用空间：聚合 Media.Size（字节）
            long totalBytes = await db.Medias.AsNoTracking().SumAsync(m => (long?)m.Size) ?? 0;
            TotalSizeText = FormatBytes(totalBytes);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HomeViewModel 加载实体计数失败");
        }
    }

    private void RefreshDesktopStats()
    {
        try
        {
            // 监视文件夹：配置项总数 vs MonitorService 实际挂上 watcher 的数量
            var configured = _config.Source?.WatchFolders ?? new List<string>();
            WatchFolderTotal = configured.Count;
            WatchFolderActive = configured.Count(p => _monitorService.IsMonitoring(p));

            // 任务计数
            var allTasks = _progressService.GetAllRootTasks().ToList();
            RunningTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying);
            FailedTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Failed
                or TaskExecutionStatus.Timeout);

            // 待处理计数（同步 + 短查询）
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
            OnPropertyChanged(nameof(PendingText));
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
