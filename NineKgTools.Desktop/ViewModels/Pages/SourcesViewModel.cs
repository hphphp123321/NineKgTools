using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体源工作台（§5.1 §10.5 P2 重构）。从原"监视文件夹列表"重构为"拖拽即识别 + 实时进度"。
///
/// **职责分离**：
/// - 媒体源（本页）：拖拽工作台，展示当前会话的实时识别进度卡组。重启清空。
/// - 监视文件夹（<see cref="WatchFoldersViewModel"/>）：长期跟踪配置，从 header 按钮跳转。
///
/// **拖拽进度跟踪**：订阅 <see cref="DragDropDispatcher.TaskSubmitted"/>，每次有任何路径
/// （主窗外层 DragOverlay 或本页内部拖拽区）成功提交识别任务，把 taskId 加进 TrackedTasks。
/// 500ms DispatcherTimer 调每个卡的 NotifyAll() 让 UI 跟手。重启后清空（不持久化，靠任务页查历史）。
/// </summary>
public partial class SourcesViewModel : PageViewModelBase
{
    /// <summary>进度卡组上限。超过后老的卡会被裁掉。</summary>
    private const int MaxKeptCards = 20;

    private readonly NavigationService _navigation;
    private readonly DragDropDispatcher _dispatcher;
    private readonly TaskProgressService _progressService;
    private DispatcherTimer? _pollTimer;

    public override string Title => "媒体源";

    /// <summary>正在跟踪的进度卡组（最新提交的在最前）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTasks))]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    private ObservableCollection<TaskItemViewModel> _trackedTasks = new();

    /// <summary>用于 axaml 拖拽区 hover 视觉反馈（背景色 + 边框由 Style 切换）。</summary>
    [ObservableProperty]
    private bool _isDragOver;

    public bool HasTasks => TrackedTasks.Count > 0;
    public bool ShowEmpty => TrackedTasks.Count == 0;

    public SourcesViewModel(NavigationService navigation, DragDropDispatcher dispatcher,
        TaskProgressService progressService)
    {
        _navigation = navigation;
        _dispatcher = dispatcher;
        _progressService = progressService;

        TrackedTasks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTasks));
            OnPropertyChanged(nameof(ShowEmpty));
        };
    }

    public override Task OnEnterAsync()
    {
        // 订阅拖拽提交事件——同窗内任何路径（外层 DragOverlay / 本页内部拖拽区）提交都会触发
        _dispatcher.TaskSubmitted += OnTaskSubmitted;

        // 500ms 轮询：让现有卡的 progress 字段刷新。比 BackgroundTasksPage 的 500ms 一致，避免高频卡顿。
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pollTimer.Tick += (_, _) => RefreshTasks();
        _pollTimer.Start();
        return Task.CompletedTask;
    }

    public override Task OnLeaveAsync()
    {
        _dispatcher.TaskSubmitted -= OnTaskSubmitted;
        _pollTimer?.Stop();
        _pollTimer = null;
        return Task.CompletedTask;
    }

    /// <summary>跳转到监视文件夹子页面（参考"标签 / 标签映射"模式）。</summary>
    [RelayCommand]
    private Task GoToWatchFolders() => _navigation.NavigateToAsync<WatchFoldersViewModel>();

    /// <summary>选择文件夹（macOS 触控板拖拽体验差时的 fallback 入口）。</summary>
    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var top = GetTopLevel();
        if (top?.StorageProvider is null) return;
        try
        {
            var picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择文件夹以识别",
                AllowMultiple = false,
            });
            if (picked.Count == 0) return;
            var path = picked[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            // 走 dispatcher 同样的"单文件夹 → 双卡片对话框"路径
            await _dispatcher.HandleDropAsync(new[] { path });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SourcesViewModel.PickFolder 失败");
        }
    }

    /// <summary>选择文件（多选）。</summary>
    [RelayCommand]
    private async Task PickFilesAsync()
    {
        var top = GetTopLevel();
        if (top?.StorageProvider is null) return;
        try
        {
            var picked = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择文件以识别",
                AllowMultiple = true,
            });
            if (picked.Count == 0) return;

            var paths = picked.Select(p => p.TryGetLocalPath())
                              .Where(p => !string.IsNullOrEmpty(p))
                              .Cast<string>()
                              .ToList();
            if (paths.Count == 0) return;

            await _dispatcher.HandleDropAsync(paths);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SourcesViewModel.PickFiles 失败");
        }
    }

    /// <summary>清空已完成 / 失败 / 取消的卡片，保留运行中。</summary>
    [RelayCommand]
    private void ClearCompleted()
    {
        for (int i = TrackedTasks.Count - 1; i >= 0; i--)
        {
            if (TrackedTasks[i].IsCompleted)
                TrackedTasks.RemoveAt(i);
        }
    }

    /// <summary>清空所有卡片（含运行中——只是从 UI 移除，任务本身仍在后台跑）。</summary>
    [RelayCommand]
    private void ClearAll() => TrackedTasks.Clear();

    /// <summary>移除单张卡（不取消任务）。</summary>
    [RelayCommand]
    private void RemoveCard(TaskItemViewModel? item)
    {
        if (item is null) return;
        TrackedTasks.Remove(item);
    }

    /// <summary>SourcesPage code-behind 在 Drop 事件里调，把 paths 转给 dispatcher 处理。</summary>
    public Task HandleDroppedPathsAsync(IReadOnlyList<string> paths)
        => _dispatcher.HandleDropAsync(paths);

    private void OnTaskSubmitted(object? sender, DropSubmittedEventArgs e)
    {
        // dispatcher 可能从非 UI 线程 fire（IdentifyXxx 是 async）；切回 UI 线程加卡
        Dispatcher.UIThread.Post(() => AddTaskCard(e.TaskId));
    }

    private void AddTaskCard(string taskId)
    {
        try
        {
            // 防 dup（同一 task 不应该被加两次）
            if (TrackedTasks.Any(t => t.TaskId == taskId)) return;

            var progress = _progressService.GetProgress(taskId);
            if (progress is null)
            {
                Log.Debug("SourcesViewModel.AddTaskCard：TaskProgressService 找不到 {TaskId}", taskId);
                return;
            }

            // 最新插入到顶部
            TrackedTasks.Insert(0, new TaskItemViewModel(progress));

            // 超过上限裁底部老的
            while (TrackedTasks.Count > MaxKeptCards)
                TrackedTasks.RemoveAt(TrackedTasks.Count - 1);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SourcesViewModel.AddTaskCard 失败：{TaskId}", taskId);
        }
    }

    private void RefreshTasks()
    {
        try
        {
            foreach (var card in TrackedTasks)
            {
                card.NotifyAll();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "SourcesViewModel.RefreshTasks 失败");
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
