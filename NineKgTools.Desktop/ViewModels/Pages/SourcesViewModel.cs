using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体源页（监视文件夹列表）。展示 Config.Source.WatchFolders 的配置项 + MonitorService 实时状态。
/// 添加：原生 FolderPicker → 加入 WatchFolders + 提交 IdentifyBatchMedia（startMonitoringAfterCompletion=true）
/// 删除：MonitorService.StopMonitoring + 从 WatchFolders 移除 + SaveConfig
/// 重新扫描：IdentifyBatchMedia(folder, startMonitoringAfterCompletion=false) 触发一次性识别
/// 拖入文件夹到主窗任意位置 → 弹双卡片选择，"加入监视"路径会落到这页
/// </summary>
public partial class SourcesViewModel : PageViewModelBase
{
    private readonly Config _config;
    private readonly FilesService _filesService;
    private readonly MonitorService _monitorService;
    private DispatcherTimer? _refreshTimer;

    public override string Title => "媒体源";

    [ObservableProperty]
    private ObservableCollection<WatchFolderItemViewModel> _folders = new();

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _showEmpty = true;

    [ObservableProperty]
    private string? _statusMessage;

    public SourcesViewModel(Config config, FilesService filesService, MonitorService monitorService)
    {
        _config = config;
        _filesService = filesService;
        _monitorService = monitorService;
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

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var configured = _config.Source?.WatchFolders ?? new List<string>();

            // 差量更新：保留现有 VM 实例（防止 ItemsControl 重渲闪屏）
            // 1. 移除已不在配置里的
            for (int i = Folders.Count - 1; i >= 0; i--)
            {
                if (!configured.Contains(Folders[i].Path)) Folders.RemoveAt(i);
            }
            // 2. 加入新配置项
            foreach (var path in configured)
            {
                if (Folders.All(f => f.Path != path))
                {
                    Folders.Add(new WatchFolderItemViewModel(path));
                }
            }
            // 3. 全部刷新状态
            foreach (var f in Folders) f.RefreshFrom(_monitorService);

            ShowEmpty = Folders.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SourcesViewModel.Refresh 失败");
        }
    }

    /// <summary>仅刷新现有项的运行状态——轮询时用，避免动 ObservableCollection。</summary>
    private void RefreshStatesOnly()
    {
        try
        {
            foreach (var f in Folders) f.RefreshFrom(_monitorService);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SourcesViewModel.RefreshStatesOnly 失败");
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
            Log.Information("SourcesViewModel 加入监视：{Path}, TaskId={TaskId}", path, taskId);

            // 立刻插入新行
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

    /// <summary>
    /// 触发一次性强制重新识别（绕过 MediaSource.InDatabase 短路）。
    /// 用 SkipCache=true 让识别流程重新爬取网页 + 重新下载 Poster/Pictures——
    /// 修复早期 bug 留下的"Media.Poster=null"或"cache 文件丢失"等历史脏数据。
    /// </summary>
    [RelayCommand]
    private async Task RescanAsync(WatchFolderItemViewModel? item)
    {
        if (item is null || !item.DirectoryExists) return;
        try
        {
            var options = _config.Identification?.ToIdentificationOptions() ?? new IdentificationOptions();
            options.SkipCache = true;  // 关键：绕过 GetMediaByPath 的"已存在"短路，重走完整识别
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
            // Win 用 explorer.exe；其他平台 try/catch 失败不报
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
