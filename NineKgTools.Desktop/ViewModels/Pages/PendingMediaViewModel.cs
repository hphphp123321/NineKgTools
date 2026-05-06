using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Source;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class PendingMediaViewModel : PageViewModelBase
{
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly SourceService _sourceService;
    private readonly PendingIdentificationService _pendingService;
    private readonly FilesService _filesService;

    public override string Title => "待处理";

    [ObservableProperty]
    private ObservableCollection<PendingMediaItemViewModel> _pendingIdentifyItems = new();

    [ObservableProperty]
    private ObservableCollection<PendingMediaItemViewModel> _pendingDatabaseItems = new();

    [ObservableProperty]
    private int _pendingIdentifyCount;

    [ObservableProperty]
    private int _pendingDatabaseCount;

    /// <summary>选中 Tab 索引：0=待识别 / 1=待入库</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showIdentifyEmpty;

    [ObservableProperty]
    private bool _showDatabaseEmpty;

    public PendingMediaViewModel(
        IDbContextFactory<MediaDbContext> dbFactory,
        SourceService sourceService,
        PendingIdentificationService pendingService,
        FilesService filesService)
    {
        _dbFactory = dbFactory;
        _sourceService = sourceService;
        _pendingService = pendingService;
        _filesService = filesService;
    }

    public override Task OnEnterAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            // Tab 1: 待识别 = MediaSources where Identified=false
            await using var db = await _dbFactory.CreateDbContextAsync();
            var pendingIdentify = await db.MediaSources
                .AsNoTracking()
                .Where(s => !s.Identified)
                .OrderBy(s => s.Id)
                .ToListAsync();

            PendingIdentifyItems = new ObservableCollection<PendingMediaItemViewModel>(
                pendingIdentify.Select(s => new PendingMediaItemViewModel(s)));
            PendingIdentifyCount = pendingIdentify.Count;
            ShowIdentifyEmpty = pendingIdentify.Count == 0;

            // Tab 2: 待入库 = PendingIdentificationService.GetAllPendingAsync
            var pendingDatabase = await _pendingService.GetAllPendingAsync();
            PendingDatabaseItems = new ObservableCollection<PendingMediaItemViewModel>(
                pendingDatabase.Select(t => new PendingMediaItemViewModel(t.source, t.media)));
            PendingDatabaseCount = pendingDatabase.Count;
            ShowDatabaseEmpty = pendingDatabase.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PendingMediaViewModel 刷新失败");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ========== 待识别 Tab 行操作 ==========

    [RelayCommand]
    private async Task IdentifyAsync(PendingMediaItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            await _filesService.IdentifySingleMedia(item.FullPath);
            Log.Information("已提交识别任务：{Path}", item.FullPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "提交识别任务失败：{Path}", item.FullPath);
        }
    }

    [RelayCommand]
    private Task ManualAddAsync(PendingMediaItemViewModel? item)
    {
        if (item is null) return Task.CompletedTask;
        // TODO Phase 2: 接入 ManualAddMediaHelper 流程
        Log.Information("手动添加（待 Phase 2 接入 ManualAddMediaHelper）：{Path}", item.FullPath);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DiscardIdentifyAsync(PendingMediaItemViewModel? item)
    {
        if (item is null) return;
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "丢弃媒体源",
            message: "丢弃后该媒体源将不再出现在待识别列表中。已识别且入库的媒体不受影响。",
            intent: DialogIntent.Destructive,
            targetName: item.DisplayName,
            confirmText: "确认丢弃");
        if (!confirmed) return;

        try
        {
            await _sourceService.RemoveMediaSourceAsync(item.Source);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "丢弃媒体源失败：{Path}", item.FullPath);
        }
    }

    // ========== 待入库 Tab 行操作 ==========

    [RelayCommand]
    private async Task ApproveDatabaseAsync(PendingMediaItemViewModel? item)
    {
        if (item is null || item.IdentifiedMedia is null) return;
        try
        {
            // 把识别得到的 MediaBase 正式写入数据库；FilesService.AddMediaToDatabase 内部
            // 会同时维护 Identified/InDatabase 标志 + 清理 PendingIdentification 记录
            await _filesService.AddMediaToDatabase(item.IdentifiedMedia);
            Log.Information("已入库：{Title}", item.IdentifiedMedia.Title);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "入库失败：{Path}", item.FullPath);
        }
    }

    [RelayCommand]
    private async Task ReidentifyAsync(PendingMediaItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            // 重新走识别 — 旧 PendingIdentification 会被新结果覆盖
            await _filesService.IdentifySingleMedia(item.FullPath);
            Log.Information("已重新提交识别任务：{Path}", item.FullPath);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重新识别失败：{Path}", item.FullPath);
        }
    }

    [RelayCommand]
    private async Task DiscardDatabaseAsync(PendingMediaItemViewModel? item)
    {
        if (item is null) return;
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "丢弃识别结果",
            message: "丢弃后此识别结果将不再保留，媒体源也会从待入库列表移除。",
            intent: DialogIntent.Destructive,
            targetName: item.IdentifiedTitle ?? item.DisplayName,
            confirmText: "确认丢弃");
        if (!confirmed) return;

        try
        {
            await _pendingService.RemoveBySourceIdAsync(item.SourceId);
            await _sourceService.RemoveMediaSourceAsync(item.Source);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "丢弃待入库记录失败：{Path}", item.FullPath);
        }
    }

    [RelayCommand]
    private Task PreviewDatabaseAsync(PendingMediaItemViewModel? item)
    {
        if (item is null || item.IdentifiedMedia is null) return Task.CompletedTask;
        // TODO Phase 1.3: 接入媒体详情独立窗口（只读模式预览）
        Log.Information("预览待入库（待 Phase 1.3 接入详情窗）：{Title}", item.IdentifiedMedia.Title);
        return Task.CompletedTask;
    }
}
