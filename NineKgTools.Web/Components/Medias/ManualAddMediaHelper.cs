using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Source;
using Serilog;

namespace NineKgTools.Components.Medias;

/// <summary>
/// "手动添加媒体"功能的共享入口封装。负责把一个任意路径串通成完整流程：
/// 1) 路径 → MediaSource（优先复用数据库里已有的跟踪实例）
/// 2) 重复检测：若已关联 Media 则直接导航到现有详情；否则继续
/// 3) 弹 <see cref="ManualAddMediaDialog"/> 让用户填 Title + TopCategory
/// 4) 入库成功后导航到 /media/{id}?edit=true 让用户补齐其他字段
///
/// 被 <c>UnknownPage</c>（仅后 3 步）、<c>SourcesPage</c>、<c>MediaOverviewPage</c>（完整 4 步）共用。
/// </summary>
public static class ManualAddMediaHelper
{
    /// <summary>
    /// 以一个路径为起点发起"手动添加媒体"流程。
    /// 路径可能指向文件或文件夹；构造 <see cref="MediaSource"/> 时会自动判断并推断 TopCategory。
    /// </summary>
    /// <param name="path">要关联的文件或文件夹绝对路径</param>
    /// <param name="dialogService">MudBlazor 对话框服务</param>
    /// <param name="sourceService">媒体源服务（用于去重查询）</param>
    /// <param name="mediaService">媒体服务（用于查找已有关联 Media）</param>
    /// <param name="snackbar">提示服务</param>
    /// <param name="navigation">导航服务</param>
    public static async Task OpenByPathAsync(
        string path,
        IDialogService dialogService,
        SourceService sourceService,
        MediaService mediaService,
        ISnackbar snackbar,
        NavigationManager navigation)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            snackbar.Add("路径为空，无法手动添加媒体", Severity.Warning);
            return;
        }

        MediaSource sourceForDialog;
        try
        {
            // 构造候选 MediaSource：若是文件夹，构造函数会扫描子文件推断 TopCategory
            var candidate = new MediaSource(path);

            // 查数据库里是否已有同路径的 MediaSource 记录
            var dbSource = await sourceService.FindMediaSourceAsync(candidate);

            if (dbSource != null && dbSource.InDatabase)
            {
                // 已入库：尝试找关联的 Media 直接导航过去
                var existingMediaId = await mediaService.GetMediaIdByFullPathAsync(dbSource.FullPath);
                if (existingMediaId.HasValue)
                {
                    snackbar.Add("该路径已有媒体，已跳转到详情", Severity.Info);
                    navigation.NavigateTo($"/media/{existingMediaId.Value}");
                    return;
                }

                // 边缘情况：InDatabase=true 但实际找不到 Media（关联损坏）
                // 降级为"把该源当未入库处理"，允许用户重新建媒体
                Log.Warning("路径 {Path} 的 MediaSource 标记为 InDatabase=true 但未找到关联 Media，降级为手动添加", dbSource.FullPath);
            }

            // 未入库：用数据库里跟踪的那份（若存在），否则用新构造的候选
            sourceForDialog = dbSource ?? candidate;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手动添加媒体前的路径检测失败: {Path}", path);
            snackbar.Add("检测路径状态失败，请稍后重试。", Severity.Error);
            return;
        }

        // 弹出对话框（参数传 MediaSource）
        var parameters = new DialogParameters
        {
            { nameof(ManualAddMediaDialog.Source), sourceForDialog }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        var dialog = await dialogService.ShowAsync<ManualAddMediaDialog>(
            "手动添加媒体", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: ManualAddMediaResult ok })
        {
            snackbar.Add(
                ok.FullyFilled ? "已创建媒体" : "已创建媒体，即将进入编辑",
                Severity.Success);

            // FullyFilled=true 表示用户在对话框里就填完了选填项，不再强制切到编辑模式
            var suffix = ok.FullyFilled ? string.Empty : "?edit=true";
            navigation.NavigateTo($"/media/{ok.MediaId}{suffix}");
        }
    }
}
