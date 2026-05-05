using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class HomeViewModel : PageViewModelBase
{
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;

    public override string Title => "首页";

    [ObservableProperty]
    private int _mediaCount;

    [ObservableProperty]
    private int _pendingIdentifyCount;

    [ObservableProperty]
    private int _pendingDatabaseCount;

    public HomeViewModel(IDbContextFactory<MediaDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public override async Task OnEnterAsync()
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            MediaCount = await db.Medias.CountAsync();
            // PendingIdentifyCount / PendingDatabaseCount 待 Phase 1.5 接入
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HomeViewModel 加载计数失败");
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
