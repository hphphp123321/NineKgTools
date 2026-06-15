using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 监视文件夹列表中每一行的 VM。包装路径 + MonitorService 实时状态。
/// 由 <see cref="SourcesViewModel"/> 在轮询时构造或更新。
/// </summary>
public partial class WatchFolderItemViewModel : ObservableObject
{
    public string Path { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusIcon))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    private MonitorState _state = MonitorState.Pending;

    [ObservableProperty]
    private int _processedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private string? _startTimeText;

    public WatchFolderItemViewModel(string path)
    {
        Path = path;
    }

    public string FolderName
    {
        get
        {
            try { return System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar)); }
            catch { return Path; }
        }
    }

    public bool DirectoryExists => System.IO.Directory.Exists(Path);

    public string StatusText => State switch
    {
        MonitorState.Active => "监控中",
        MonitorState.Stopped => "已停止",
        MonitorState.Missing => "路径不存在",
        _ => "等待启动",
    };

    public string StatusIcon => State switch
    {
        MonitorState.Active => "●",
        MonitorState.Stopped => "○",
        MonitorState.Missing => "⚠",
        _ => "·",
    };

    public IBrush? StatusBrush
    {
        get
        {
            string key = State switch
            {
                MonitorState.Active => "SystemFillColorSuccessBrush",
                MonitorState.Missing => "SystemFillColorCriticalBrush",
                MonitorState.Stopped => "TextFillColorTertiaryBrush",
                _ => "SystemFillColorAttentionBrush",
            };
            // ResourceLookup 沿 Styles 链搜索，FluentAvalonia 主题 brush 才命中；
            // 旧的 Application.Current.Resources.TryGetResource 只搜 App 字典会 miss → 返回 null
            // → 状态徽文字 / 状态点变透明（"test 右边空灰框" bug）。见 CLAUDE.md §4.8。
            return ResourceLookup.Brush(key);
        }
    }

    public string StatsText
    {
        get
        {
            if (State == MonitorState.Missing) return "请检查路径是否被移动 / 删除";
            if (ProcessedCount == 0 && FailedCount == 0)
                return State == MonitorState.Active ? "启动后尚未触发识别" : "暂无统计";
            return FailedCount == 0
                ? $"已处理 {ProcessedCount}"
                : $"已处理 {ProcessedCount} · 失败 {FailedCount}";
        }
    }

    /// <summary>
    /// 用 MonitorService 的实时状态刷新本行。
    /// </summary>
    public void RefreshFrom(MonitorService monitor)
    {
        if (!System.IO.Directory.Exists(Path))
        {
            State = MonitorState.Missing;
            ProcessedCount = 0;
            FailedCount = 0;
            StartTimeText = null;
            return;
        }

        if (monitor.IsMonitoring(Path))
        {
            State = MonitorState.Active;
            var stats = monitor.GetMonitoringStats(Path);
            if (stats != null)
            {
                ProcessedCount = stats.ProcessedCount;
                FailedCount = stats.FailedCount;
                StartTimeText = stats.StartTime.ToLocalTime().ToString("HH:mm:ss");
            }
        }
        else
        {
            State = MonitorState.Stopped;
            ProcessedCount = 0;
            FailedCount = 0;
            StartTimeText = null;
        }
    }
}

/// <summary>监视文件夹的 4 种实时状态。</summary>
public enum MonitorState
{
    /// <summary>等待 MonitorService 启动监视——FilesService.StartProcessConfiguredFolders 还没跑到。</summary>
    Pending,

    /// <summary>FileSystemWatcher 已挂载，文件加入会自动识别。</summary>
    Active,

    /// <summary>用户暂停或启动失败——配置在 yaml 里，但 MonitorService 没有该路径的 watcher。</summary>
    Stopped,

    /// <summary>配置存在但路径不存在——常见于盘符断开 / 文件夹被删。</summary>
    Missing,
}
