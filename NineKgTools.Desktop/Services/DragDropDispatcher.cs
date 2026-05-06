using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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

    public DragDropDispatcher(Config config, FilesService filesService)
    {
        _config = config;
        _filesService = filesService;
    }

    /// <summary>
    /// 解析 DragEventArgs 拿到本地文件路径列表。返回空列表表示无法解析或为远端 URL。
    /// </summary>
    public static List<string> ExtractLocalPaths(DragEventArgs e)
    {
        var paths = new List<string>();
        try
        {
            var files = e.Data.GetFiles();
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
            await _filesService.IdentifySingleMedia(path);
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
                    await _filesService.IdentifyBatchMedia(p, startMonitoringAfterCompletion: false);
                else
                    await _filesService.IdentifySingleMedia(p);
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
        await _filesService.IdentifyBatchMedia(path, startMonitoringAfterCompletion: true);
        Log.Information("拖入文件夹已加入监视：{Path}", path);
    }

    private async Task IdentifyOnceAsync(string path)
    {
        await _filesService.IdentifyBatchMedia(path, startMonitoringAfterCompletion: false);
        Log.Information("拖入文件夹已一次性识别（不加监视）：{Path}", path);
    }
}

/// <summary>用户在拖入文件夹后选择的动作。</summary>
public enum FolderDragAction
{
    AddToWatch,
    IdentifyOnce,
}
