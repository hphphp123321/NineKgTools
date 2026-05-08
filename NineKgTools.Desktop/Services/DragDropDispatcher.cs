using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 拖拽事件分发器。把"用户拖了 N 个项到主窗"翻译成具体动作：
/// - 单文件 → 直接走识别 / 入库流程（最快路径，无确认对话框）
/// - 单文件夹 → 弹双卡片选择（加入监视 vs 一次性识别）
/// - 多个项 → 批量识别确认对话框
///
/// 路径解析失败 / 权限拒绝走 InfoBar 提示，**不**用对话框打断流程。
/// </summary>
public class DragDropDispatcher
{
    private readonly Config _config;
    private readonly FilesService _filesService;

    /// <summary>
    /// 任何路径成功提交识别任务后 fire（IdentifySingleMedia / IdentifyBatchMedia 返回非空 taskId 即触发）。
    /// SourcesViewModel（媒体源工作台）订阅它实时跟踪拖入任务的进度。
    /// 同时供主窗外层 DragOverlay 路径使用——用户在任意页面拖入，进 SourcesPage 时仍能看到本次会话内的进度。
    /// </summary>
    public event EventHandler<DropSubmittedEventArgs>? TaskSubmitted;

    public DragDropDispatcher(Config config, FilesService filesService)
    {
        _config = config;
        _filesService = filesService;
    }

    private void RaiseTaskSubmitted(string taskId, string path, DropTaskKind kind)
    {
        try { TaskSubmitted?.Invoke(this, new DropSubmittedEventArgs(taskId, path, kind)); }
        catch (Exception ex) { Log.Warning(ex, "DragDropDispatcher.TaskSubmitted handler 异常"); }
    }

    /// <summary>
    /// 解析 DragEventArgs 拿到本地文件路径列表。返回空列表表示无法解析或为远端 URL。
    /// Avalonia 12 改用 DataTransfer + DataFormat（旧 e.Data + DataFormats 已 obsolete）。
    /// </summary>
    public static List<string> ExtractLocalPaths(DragEventArgs e)
    {
        var paths = new List<string>();
        try
        {
            if (!e.DataTransfer.Contains(DataFormat.File)) return paths;
            var files = e.DataTransfer.TryGetFiles();
            if (files is null) return paths;
            foreach (var item in files)
            {
                var local = item.TryGetLocalPath();
                if (!string.IsNullOrEmpty(local)) paths.Add(local);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DragDrop 提取路径失败");
        }
        return paths;
    }

    /// <summary>
    /// 用户放下文件后调用。根据数量 / 类型决定下一步流程。
    /// 返回值：true 表示已处理（哪怕用户取消），false 表示无任何路径可处理。
    /// </summary>
    public async Task<bool> HandleDropAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return false;

        // 单 item：单文件直接处理；单文件夹弹双卡片
        if (paths.Count == 1)
        {
            var p = paths[0];
            if (Directory.Exists(p))
            {
                return await HandleSingleFolderAsync(p);
            }
            if (File.Exists(p))
            {
                return await HandleSingleFileAsync(p);
            }
            Log.Warning("拖入的路径既不是文件也不是文件夹：{Path}", p);
            return false;
        }

        // 多 item：分类汇总后弹批量确认
        return await HandleMultipleAsync(paths);
    }

    private async Task<bool> HandleSingleFolderAsync(string path)
    {
        // Phase 3 第 2 轮：弹双卡片对话框（加入监视 / 一次性识别）
        var action = await DragDropFolderActionDialog.ShowAsync(path);
        if (action is null) return true; // 用户取消

        try
        {
            switch (action.Value)
            {
                case FolderDragAction.AddToWatch:
                    await AddToWatchAsync(path);
                    break;
                case FolderDragAction.IdentifyOnce:
                    await IdentifyOnceAsync(path);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理拖入文件夹失败：{Path}", path);
        }
        return true;
    }

    private async Task<bool> HandleSingleFileAsync(string path)
    {
        // 单文件：直接进识别队列。
        try
        {
            var taskId = await _filesService.IdentifySingleMedia(path);
            if (!string.IsNullOrEmpty(taskId))
                RaiseTaskSubmitted(taskId, path, DropTaskKind.SingleFile);
            Log.Information("拖入单文件已加入识别队列：{Path}", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "拖入单文件识别失败：{Path}", path);
        }
        return true;
    }

    private async Task<bool> HandleMultipleAsync(IReadOnlyList<string> paths)
    {
        var validPaths = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (validPaths.Count == 0)
        {
            Log.Warning("拖入的多个项目均无效");
            return false;
        }

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "批量识别",
            message: $"将识别拖入的 {validPaths.Count} 个项目（混合文件 + 文件夹）。文件夹会按一次性识别处理，不加入监视。",
            intent: DialogIntent.Affirmative,
            confirmText: "开始识别",
            affectedCount: validPaths.Count);
        if (!confirmed) return true;

        foreach (var p in validPaths)
        {
            try
            {
                if (Directory.Exists(p))
                {
                    var taskId = await _filesService.IdentifyBatchMedia(p, startMonitoringAfterCompletion: false);
                    if (!string.IsNullOrEmpty(taskId))
                        RaiseTaskSubmitted(taskId, p, DropTaskKind.BatchFolder);
                }
                else
                {
                    var taskId = await _filesService.IdentifySingleMedia(p);
                    if (!string.IsNullOrEmpty(taskId))
                        RaiseTaskSubmitted(taskId, p, DropTaskKind.SingleFile);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "批量识别中跳过失败项：{Path}", p);
            }
        }
        return true;
    }

    private async Task AddToWatchAsync(string path)
    {
        // 加入 watch_folders + 触发监视任务
        if (_config.Source is null) _config.Source = new SourceConfig();
        if (!_config.Source.WatchFolders.Contains(path))
        {
            _config.Source.WatchFolders.Add(path);
            await _config.SaveConfig();
        }
        var taskId = await _filesService.IdentifyBatchMedia(path, startMonitoringAfterCompletion: true);
        if (!string.IsNullOrEmpty(taskId))
            RaiseTaskSubmitted(taskId, path, DropTaskKind.WatchFolder);
        Log.Information("拖入文件夹已加入监视：{Path}", path);
    }

    private async Task IdentifyOnceAsync(string path)
    {
        var taskId = await _filesService.IdentifyBatchMedia(path, startMonitoringAfterCompletion: false);
        if (!string.IsNullOrEmpty(taskId))
            RaiseTaskSubmitted(taskId, path, DropTaskKind.BatchFolder);
        Log.Information("拖入文件夹已一次性识别（不加监视）：{Path}", path);
    }

    /// <summary>
    /// 强制 SkipCache 重新识别整个文件夹——给 IPC 命令 / SourcesPage Rescan 用。
    /// 绕过 GetMediaByPath 的"已存在"短路，让 DLsite 重新爬取 + 重新下载图片。
    /// 修复历史脏数据（Media.Poster=null / cache 文件丢失）。
    /// 返回提交后的父任务 TaskId（失败返回 null）。CLI 一次性命令的调用方可拿来轮询完成。
    /// </summary>
    public async Task<string?> RescanFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            Log.Warning("RescanFolderAsync: 文件夹不存在：{Path}", folderPath);
            return null;
        }
        try
        {
            var options = _config.Identification?.ToIdentificationOptions() ?? new IdentificationOptions();
            options.SkipCache = true;
            var taskId = await _filesService.IdentifyBatchMedia(folderPath, options, startMonitoringAfterCompletion: false);
            Log.Information("RescanFolderAsync 已提交（SkipCache=true）：{Path}, TaskId={TaskId}", folderPath, taskId);
            return taskId;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RescanFolderAsync 失败：{Path}", folderPath);
            return null;
        }
    }
}

/// <summary>用户在拖入文件夹后选择的动作。</summary>
public enum FolderDragAction
{
    AddToWatch,
    IdentifyOnce,
}

/// <summary>用于 TaskSubmitted 事件区分提交来源。</summary>
public enum DropTaskKind
{
    /// <summary>单文件直接识别。</summary>
    SingleFile,
    /// <summary>文件夹一次性识别（不加监视）。</summary>
    BatchFolder,
    /// <summary>文件夹加入长期监视。</summary>
    WatchFolder,
}

/// <summary>
/// DragDropDispatcher.TaskSubmitted 事件参数：包含 task id、源路径和提交类型，
/// 让订阅方（SourcesViewModel）能展示"识别中：xxx"卡片并轮询进度。
/// </summary>
public sealed class DropSubmittedEventArgs : EventArgs
{
    public string TaskId { get; }
    public string Path { get; }
    public DropTaskKind Kind { get; }

    public DropSubmittedEventArgs(string taskId, string path, DropTaskKind kind)
    {
        TaskId = taskId;
        Path = path;
        Kind = kind;
    }
}
