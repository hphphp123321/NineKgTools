using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly Config _config;
    private readonly MonitorService _monitorService;
    private readonly TaskProgressService _progressService;
    private DispatcherTimer? _refreshTimer;

    public override string Title => "首页";

    [ObservableProperty]
    private int _mediaCount;

    [ObservableProperty]
    private int _watchFolderTotal;

    [ObservableProperty]
    private int _watchFolderActive;

    [ObservableProperty]
    private int _runningTaskCount;

    [ObservableProperty]
    private int _failedTaskCount;

    public string WatchFolderText => WatchFolderTotal == 0
        ? "未配置"
        : $"{WatchFolderActive} / {WatchFolderTotal} 监控中";

    public string RunningTaskText => RunningTaskCount == 0 ? "无运行中任务" : $"{RunningTaskCount} 个运行中";
    public string FailedTaskText => FailedTaskCount == 0 ? "暂无失败" : $"{FailedTaskCount} 个失败需关注";
    public bool HasFailedTasks => FailedTaskCount > 0;

    public HomeViewModel(IDbContextFactory<MediaDbContext> dbFactory,
        Config config, MonitorService monitorService, TaskProgressService progressService)
    {
        _dbFactory = dbFactory;
        _config = config;
        _monitorService = monitorService;
        _progressService = progressService;
    }

    public override async Task OnEnterAsync()
    {
        await RefreshMediaCountAsync();
        RefreshDesktopStats();

        // 5s 轮询桌面端实时状态（监控状态 + 任务计数）。媒体总数变化频率低，进入页面时拉一次即可
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

    private async Task RefreshMediaCountAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            MediaCount = await db.Medias.CountAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HomeViewModel 加载媒体计数失败");
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

            // 任务计数：运行中（含 Pending / Retrying）+ 失败（含 Timeout）
            var allTasks = _progressService.GetAllRootTasks().ToList();
            RunningTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Running
                or TaskExecutionStatus.Pending
                or TaskExecutionStatus.Retrying);
            FailedTaskCount = allTasks.Count(t => t.Status is TaskExecutionStatus.Failed
                or TaskExecutionStatus.Timeout);

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

    // ========== Phase 1.7 演示：4 种 Intent 对话框预览 ==========

    [RelayCommand]
    private async Task ShowInfoDialog()
    {
        await NineKgConfirmDialog.ShowAsync(null,
            title: "确认操作",
            message: "确认执行此操作吗？",
            intent: DialogIntent.Info);
    }

    [RelayCommand]
    private async Task ShowAffirmativeDialog()
    {
        await NineKgConfirmDialog.ShowAsync(null,
            title: "批量入库",
            message: "将把 12 条已识别的媒体入库到主媒体库。",
            intent: DialogIntent.Affirmative,
            confirmText: "立即入库");
    }

    [RelayCommand]
    private async Task ShowDestructiveDialog()
    {
        await NineKgConfirmDialog.ShowAsync(null,
            title: "确认删除",
            message: "你将永久删除此媒体及其全部关联数据（标签 / 评分 / 收藏夹）。",
            intent: DialogIntent.Destructive,
            targetName: "视频名称_完整版_第二季_第 5 集.mp4");
    }

    [RelayCommand]
    private async Task ShowDestructiveBatchDialog()
    {
        await NineKgConfirmDialog.ShowAsync(null,
            title: "批量删除",
            message: "你将永久删除选中的 23 条媒体及其全部关联数据。",
            intent: DialogIntent.DestructiveBatch,
            affectedCount: 23,
            targetItems: new[]
            {
                "视频名称 1.mp4",
                "视频名称 2.mp4",
                "视频名称 3.mp4",
                "等共 23 项"
            });
    }
}
