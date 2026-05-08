using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 监视文件夹页（§5.1 §10.5 P2 拆分）。展示 Config.Source.WatchFolders 配置项 +
/// MonitorService 实时状态，以及 add/remove/rescan/openInExplorer 行操作。
///
/// 由 SourcesViewModel（媒体源工作台）的 header 按钮跳转进入；本页 header 有
/// "← 媒体源"返回按钮跳回。模仿"标签 / 标签映射"模式：主入口在侧栏（媒体源），
/// 子配置页（监视文件夹）从 header 进入，不占侧栏。
/// </summary>
public partial class WatchFoldersViewModel : PageViewModelBase
{
    private readonly Config _config;
    private readonly FilesService _filesService;
    private readonly MonitorService _monitorService;
    private readonly NavigationService _navigation;
    private DispatcherTimer? _refreshTimer;

    public override string Title => "监视文件夹";

    [ObservableProperty]
    private ObservableCollection<WatchFolderItemViewModel> _folders = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showEmpty = true;

    [ObservableProperty]
    private string? _statusMessage;

    public WatchFoldersViewModel(Config config, FilesService filesService,
        MonitorService monitorService, NavigationService navigation)
    {
        _config = config;
        _filesService = filesService;
        _monitorService = monitorService;
        _navigation = navigation;
    }

    public override Task OnEnterAsync()
    {
        Refresh();
        // 5s 轮询：MonitorService 状态可能变（外部停了 watcher、文件被识别等），保持 UI 跟手
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += (_, _) => RefreshStatesOnly();
        _refreshTimer.Start();
        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        return Task.CompletedTask;
    }

    /// <summary>从 header 返回按钮触发，跳回媒体源工作台。</summary>
    [RelayCommand]
    private Task GoBackToSources() => _navigation.NavigateToAsync<SourcesViewModel>();

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var configured = _config.Source?.WatchFolders ?? new List<string>();

            // 差量更新：保留现有 VM 实例（防止 ItemsControl 重渲闪屏）
            for (int i = Folders.Count - 1; i >= 0; i--)
            {
                if (!configured.Contains(Folders[i].Path)) Folders.RemoveAt(i);
            }
            foreach (var path in configured)
            {
                if (Folders.All(f => f.Path != path))
                {
                    Folders.Add(new WatchFolderItemViewModel(path));
                }
            }
            foreach (var f in Folders) f.RefreshFrom(_monitorService);

            ShowEmpty = Folders.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WatchFoldersViewModel.Refresh 失败");
        }
    }

    private void RefreshStatesOnly()
    {
        try
        {
            foreach (var f in Folders) f.RefreshFrom(_monitorService);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WatchFoldersViewModel.RefreshStatesOnly 失败");
        }
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is null)
            {
                StatusMessage = "无法打开文件夹选择器";
                return;
            }

            var picked = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择要监视的文件夹",
                AllowMultiple = false,
            });
            if (picked.Count == 0) return;

            var path = picked[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            await AddPathInternalAsync(path);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddPathInternalAsync(string path)
    {
        if (_config.Source is null) _config.Source = new SourceConfig();

        if (_config.Source.WatchFolders.Contains(path))
        {
            StatusMessage = "该路径已在监视列表中";
            return;
        }

        try
        {
            _config.Source.WatchFolders.Add(path);
            await _config.SaveConfig();

            var taskId = await _filesService.IdentifyBatchMedia(path, startMonitoringAfterCompletion: true);
            StatusMessage = $"已加入监视：{System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar))}（任务 {taskId}）";
            Log.Information("WatchFoldersViewModel 加入监视：{Path}, TaskId={TaskId}", path, taskId);

            var item = new WatchFolderItemViewModel(path);
            item.RefreshFrom(_monitorService);
            Folders.Add(item);
            ShowEmpty = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加监视文件夹失败：{Path}", path);
            StatusMessage = "添加失败，请稍后重试";
        }
    }

    [RelayCommand]
    private async Task RemoveAsync(WatchFolderItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "移除监视",
            message: $"将停止监控该文件夹并从配置中删除。**已识别入库的媒体不会被删除**，仅停止后续新文件的自动识别。",
            intent: DialogIntent.Destructive,
            targetName: item.Path,
            confirmText: "确认移除");
        if (!confirmed) return;

        try
        {
            await _monitorService.StopMonitoring(item.Path);
            if (_config.Source is not null)
            {
                _config.Source.WatchFolders.RemoveAll(p =>
                    string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase));
                await _config.SaveConfig();
            }
            Folders.Remove(item);
            ShowEmpty = Folders.Count == 0;
            StatusMessage = "已移除";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除监视失败：{Path}", item.Path);
            StatusMessage = "移除失败，请稍后重试";
        }
    }

    [RelayCommand]
    private async Task RescanAsync(WatchFolderItemViewModel? item)
    {
        if (item is null || !item.DirectoryExists) return;
        try
        {
            var options = _config.Identification?.ToIdentificationOptions() ?? new IdentificationOptions();
            options.SkipCache = true;
            var taskId = await _filesService.IdentifyBatchMedia(item.Path, options, startMonitoringAfterCompletion: false);
            StatusMessage = $"已开始重新扫描：{item.FolderName}（任务 {taskId}，强制刷新）";
            Log.Information("Rescan (SkipCache=true)：{Path}, TaskId={TaskId}", item.Path, taskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重新扫描失败：{Path}", item.Path);
            StatusMessage = "重新扫描失败，请稍后重试";
        }
    }

    [RelayCommand]
    private void OpenInExplorer(WatchFolderItemViewModel? item)
    {
        if (item is null || !item.DirectoryExists) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = item.Path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "打开资源管理器失败：{Path}", item.Path);
        }
    }

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
