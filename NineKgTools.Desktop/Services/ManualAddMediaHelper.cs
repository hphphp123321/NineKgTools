using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Source;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// "手动添加媒体"流程的桌面端共享入口（对齐 Web 端的 ManualAddMediaHelper）。
///
/// 单一职责：
/// 1) 路径 → 候选 <see cref="MediaSource"/>（构造时自动判断文件 / 文件夹 + 推断 TopCategory）
/// 2) 重复检测：若该路径已关联入库 Media → 直接打开现有 MediaDetailWindow；否则继续
/// 3) 弹 <see cref="ManualAddMediaDialog"/> 让用户填 Title + TopCategory（可选填子分类 / 简介 / 评分）
/// 4) 入库成功 → 打开 MediaDetailWindow 让用户继续浏览 / 编辑（编辑模式待 §1.3 落地）
///
/// 调用方：
/// - <c>PendingMediaViewModel.ManualAddAsync</c>（待识别 Tab 行操作）
/// - <c>SourcesViewModel</c>（如新增"手动添加"按钮，§5.1 P3）
/// - <c>MediaOverviewViewModel</c>（PageHeader 新建媒体入口，§1.2 P0，需先选 path）
/// </summary>
public static class ManualAddMediaHelper
{
    /// <summary>
    /// 以一个文件 / 文件夹路径为起点发起手动添加流程。
    /// 失败仅 Log + 返回 null；调用方可视情况展示 Toast / Snackbar。
    /// </summary>
    /// <param name="path">要关联的文件或文件夹绝对路径</param>
    /// <param name="services">DI 容器，需提供 SourceService / MediaService / FilesService / WindowManager</param>
    /// <returns>成功创建的新媒体 Id；用户取消 / 已存在直接跳转 / 失败 → null</returns>
    public static async Task<int?> OpenByPathAsync(string path, IServiceProvider services)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Log.Warning("ManualAddMediaHelper: 路径为空，跳过");
            return null;
        }

        var sourceService = services.GetRequiredService<SourceService>();
        var mediaService = services.GetRequiredService<MediaService>();
        var filesService = services.GetRequiredService<FilesService>();
        var navigationService = services.GetRequiredService<NavigationService>();

        MediaSource sourceForDialog;
        try
        {
            // 构造候选 MediaSource——文件夹会扫子文件推断 TopCategory
            var candidate = new MediaSource(path);

            // 数据库重复检测
            var dbSource = await sourceService.FindMediaSourceAsync(candidate);

            if (dbSource is { InDatabase: true })
            {
                // 已入库：找关联 Media 直接打开详情，避免重复创建
                var existingId = await mediaService.GetMediaIdByFullPathAsync(dbSource.FullPath);
                if (existingId.HasValue)
                {
                    Log.Information("ManualAddMediaHelper: 路径已入库，跳转到现有媒体 Id={Id}, Path={Path}",
                        existingId.Value, dbSource.FullPath);
                    // 默认走主窗内嵌（与 Web NavigationManager.NavigateTo($"/media/{id}") 一致）
                    var idToOpen = existingId.Value;
                    await navigationService.NavigateToAsync<NineKgTools.Desktop.ViewModels.Pages.MediaDetailViewModel>(vm =>
                    {
                        vm.Mode = NineKgTools.Desktop.ViewModels.Pages.MediaDetailMode.EmbeddedPage;
                        vm.RequestOpenDetail(idToOpen);
                    });
                    return idToOpen;
                }

                // 边缘：InDatabase=true 但找不到 Media（数据损坏）—— 降级当未入库处理
                Log.Warning("ManualAddMediaHelper: MediaSource 标记 InDatabase=true 但找不到关联 Media，降级处理 Path={Path}",
                    dbSource.FullPath);
            }

            // 未入库（或边缘损坏）：用 db 跟踪那份（若存在），否则用候选
            sourceForDialog = dbSource ?? candidate;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ManualAddMediaHelper: 路径检测失败 Path={Path}", path);
            return null;
        }

        // 弹对话框；ShowAsync 内部会调 FilesService.AddMediaToDatabase
        var result = await ManualAddMediaDialog.ShowAsync(sourceForDialog, filesService);
        if (result is null)
        {
            // 用户取消 / 入库失败（错误已通过 InfoBar 提示并 Log 过）
            return null;
        }

        // 成功：默认走主窗内嵌（与 Web NavigationManager.NavigateTo($"/media/{id}") 一致）。
        // 用户在详情页可点 [↗] 升级到独立窗
        var newId = result.MediaId;
        await navigationService.NavigateToAsync<NineKgTools.Desktop.ViewModels.Pages.MediaDetailViewModel>(vm =>
        {
            vm.Mode = NineKgTools.Desktop.ViewModels.Pages.MediaDetailMode.EmbeddedPage;
            vm.RequestOpenDetail(newId);
        });

        Log.Information("ManualAddMediaHelper: 已创建并打开媒体 Id={Id}, FullyFilled={Full}",
            result.MediaId, result.FullyFilled);

        return result.MediaId;
    }
}
