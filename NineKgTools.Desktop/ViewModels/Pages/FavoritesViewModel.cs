using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public partial class FavoritesViewModel : PageViewModelBase
{
    private readonly FavoriteService _favoriteService;
    private readonly ImageCacheService _imageCache;

    public override string Title => "收藏夹";

    [ObservableProperty]
    private ObservableCollection<FavoriteItemViewModel> _favorites = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedFavoriteName))]
    [NotifyPropertyChangedFor(nameof(SelectedFavoriteCountText))]
    [NotifyPropertyChangedFor(nameof(CanRenameSelected))]
    [NotifyPropertyChangedFor(nameof(CanDeleteSelected))]
    private FavoriteItemViewModel? _selectedFavorite;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _items = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmpty = true;

    // ========== 批量选择态 ==========

    /// <summary>是否进入批量选择模式。true 时卡片左上角 CheckBox 常驻显示，× 隐藏，
    /// header 右侧按钮组从"重命名/删除收藏夹"切到"移除选中(N)/取消选择"。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSingleActions))]
    [NotifyPropertyChangedFor(nameof(ShowBatchActions))]
    private bool _isBatchMode;

    /// <summary>当前选中媒体数。Items 内子项 IsSelected 改变不会自动通知此属性，
    /// 由 ToggleMediaSelection 命令显式触发 OnPropertyChanged。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchRemoveText))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private int _selectedCount;

    [ObservableProperty]
    private bool _isBatchRemoving;

    public bool ShowSingleActions => !IsBatchMode;
    public bool ShowBatchActions => IsBatchMode;
    public bool HasSelection => SelectedCount > 0;
    public string BatchRemoveText => $"移除 {SelectedCount} 项";

    public string SelectedFavoriteName => SelectedFavorite?.Name ?? "—";
    public string SelectedFavoriteCountText => SelectedFavorite is null
        ? ""
        : $"({Items.Count} 条媒体)";
    public bool CanRenameSelected => SelectedFavorite is { IsDefault: false };
    public bool CanDeleteSelected => SelectedFavorite is { IsDefault: false };

    public FavoritesViewModel(FavoriteService favoriteService, ImageCacheService imageCache)
    {
        _favoriteService = favoriteService;
        _imageCache = imageCache;
    }

    public override async Task OnEnterAsync()
    {
        await ReloadFavoritesAsync();

        // 默认选中默认收藏夹
        var defaultFav = Favorites.FirstOrDefault(f => f.IsDefault);
        if (defaultFav is not null)
        {
            await SelectFavoriteAsync(defaultFav);
        }
    }

    private async Task ReloadFavoritesAsync()
    {
        try
        {
            var list = await _favoriteService.GetAllFavoritesAsync();
            Favorites = new ObservableCollection<FavoriteItemViewModel>(
                list.Select(f => new FavoriteItemViewModel(f)));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载收藏夹列表失败");
        }
    }

    [RelayCommand]
    private async Task SelectFavoriteAsync(FavoriteItemViewModel? item)
    {
        if (item is null) return;

        // 切换收藏夹强制退出批量态（已选媒体属于旧收藏夹，跨收藏夹批量没意义）
        if (IsBatchMode) ExitBatchMode();

        // 取消旧选中态
        foreach (var f in Favorites) f.IsSelected = false;
        item.IsSelected = true;
        SelectedFavorite = item;

        IsLoading = true;
        try
        {
            var fav = await _favoriteService.GetMediaFavoritesAsync(item.Id);
            if (fav?.Medias is null)
            {
                Items = new ObservableCollection<MediaCardViewModel>();
            }
            else
            {
                Items = new ObservableCollection<MediaCardViewModel>(
                    fav.Medias.Select(m => new MediaCardViewModel(m, _imageCache)));
            }
            ShowEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(SelectedFavoriteCountText));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载收藏夹媒体失败：{Id}", item.Id);
            Items = new ObservableCollection<MediaCardViewModel>();
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NewFavoriteAsync()
    {
        // 不允许与已有收藏夹重名（包括默认收藏夹）
        var existingNames = Favorites.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = await InputDialog.ShowAsync(
            title: "新建收藏夹",
            message: "起一个名字（可以稍后重命名）",
            placeholder: "例如：游戏精选 / 想看 / 已通关...",
            confirmText: "创建",
            maxLength: 64,
            validate: v => !existingNames.Contains(v));
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            await _favoriteService.AddFavoriteAsync(new Favorite { Name = name });
            await ReloadFavoritesAsync();
            // 选中新建的收藏夹（按名字找，避免依赖 Last 顺序）
            var newOne = Favorites.FirstOrDefault(f => f.Name == name);
            if (newOne is not null) await SelectFavoriteAsync(newOne);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "新建收藏夹失败");
        }
    }

    [RelayCommand(CanExecute = nameof(CanRenameSelected))]
    private async Task RenameSelectedAsync()
    {
        if (SelectedFavorite is null || SelectedFavorite.IsDefault) return;
        var item = SelectedFavorite;

        var existingNames = Favorites
            .Where(f => f.Id != item.Id)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newName = await InputDialog.ShowAsync(
            title: "重命名收藏夹",
            message: $"将「{item.Name}」改为新名字",
            initialValue: item.Name,
            confirmText: "保存",
            maxLength: 64,
            validate: v => !existingNames.Contains(v) && v != item.Name);
        if (string.IsNullOrEmpty(newName) || newName == item.Name) return;

        try
        {
            // FavoriteService.UpdateFavoriteAsync 接收 Favorite 实例（id + 新 name）
            await _favoriteService.UpdateFavoriteAsync(new Favorite { Id = item.Id, Name = newName });
            await ReloadFavoritesAsync();
            // 重命名后选中态可能丢（实例换了）—— 按 Id 找回
            var refreshed = Favorites.FirstOrDefault(f => f.Id == item.Id);
            if (refreshed is not null) await SelectFavoriteAsync(refreshed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重命名收藏夹失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task RemoveMediaFromFavoriteAsync(MediaCardViewModel? media)
    {
        if (media is null || SelectedFavorite is null) return;
        var fav = SelectedFavorite;

        // 默认收藏夹的"移除"语义有歧义——意味着取消所有收藏？这里仍然走 RemoveMediaFromFavoriteAsync
        // （仅解除该 media 与默认收藏夹的关联），但用户可能没意识到——保持一致即可
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "从收藏夹移除",
            message: $"将「{media.Title}」从「{fav.Name}」中移除。**媒体本身不会被删除**，仅解除关联。",
            intent: DialogIntent.Destructive,
            targetName: media.Title,
            confirmText: "确认移除");
        if (!confirmed) return;

        try
        {
            await _favoriteService.RemoveMediaFromFavoriteAsync(fav.Id, media.Id);
            // UI 立即移除右侧媒体列表
            Items.Remove(media);
            // 同步左侧 count chip：从底层 Favorite.Medias 集合移除对应项 + 通知 VM
            var src = fav.Favorite.Medias?.FirstOrDefault(m => m.Id == media.Id);
            if (src is not null) fav.Favorite.Medias!.Remove(src);
            fav.NotifyMediaCountChanged();
            ShowEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(SelectedFavoriteCountText));
            Log.Information("已从收藏夹移除媒体：FavoriteId={FavoriteId}, MediaId={MediaId}", fav.Id, media.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "从收藏夹移除媒体失败：FavoriteId={FavoriteId}, MediaId={MediaId}", fav.Id, media.Id);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelected))]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedFavorite is null || SelectedFavorite.IsDefault) return;
        var item = SelectedFavorite;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除收藏夹",
            message: $"将解除 {item.MediaCount} 条媒体与「{item.Name}」的关联。**媒体本身不会被删除**。",
            intent: DialogIntent.Destructive,
            targetName: item.Name,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _favoriteService.RemoveFavoriteAsync(item.Id);
            await ReloadFavoritesAsync();

            // 删完后切回默认收藏夹
            var defaultFav = Favorites.FirstOrDefault(f => f.IsDefault);
            if (defaultFav is not null) await SelectFavoriteAsync(defaultFav);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除收藏夹失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ReloadFavoritesAsync();
        if (SelectedFavorite is not null)
        {
            // 找到对应新实例（reload 后 VM 实例换了）
            var match = Favorites.FirstOrDefault(f => f.Id == SelectedFavorite.Id);
            if (match is not null) await SelectFavoriteAsync(match);
        }
    }

    // ==================== 批量选择 / 移除 ====================

    /// <summary>进入批量选择模式。所有卡片显左上角 CheckBox，× 隐藏，header 切换按钮组。</summary>
    [RelayCommand]
    private void EnterBatchMode()
    {
        if (IsBatchMode) return;
        foreach (var m in Items) m.IsSelected = false;
        SelectedCount = 0;
        IsBatchMode = true;
    }

    /// <summary>退出批量选择模式 + 清空选中。</summary>
    [RelayCommand]
    private void ExitBatchMode()
    {
        if (!IsBatchMode) return;
        foreach (var m in Items) m.IsSelected = false;
        SelectedCount = 0;
        IsBatchMode = false;
    }

    /// <summary>批量态下点卡片 → 切换该 media 的选中状态。子项 IsSelected 改变后
    /// 显式 OnPropertyChanged(SelectedCount) 让 header 按钮文案 / 启用态实时更新。</summary>
    [RelayCommand]
    private void ToggleMediaSelection(MediaCardViewModel? media)
    {
        if (media is null || !IsBatchMode) return;
        media.IsSelected = !media.IsSelected;
        SelectedCount = Items.Count(i => i.IsSelected);
    }

    /// <summary>批量移除选中媒体——弹 DestructiveBatch confirm → 循环调 service。</summary>
    [RelayCommand]
    private async Task BatchRemoveSelectedAsync()
    {
        if (!IsBatchMode || SelectedFavorite is null || IsBatchRemoving) return;
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) return;

        var fav = SelectedFavorite;
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "批量从收藏夹移除",
            message: $"将解除 {selected.Count} 条媒体与「{fav.Name}」的关联。**媒体本身不会被删除**。",
            intent: DialogIntent.DestructiveBatch,
            affectedCount: selected.Count,
            targetItems: selected.Take(20).Select(m => m.Title).ToList(),
            confirmText: "确认移除");
        if (!confirmed) return;

        IsBatchRemoving = true;
        try
        {
            foreach (var media in selected)
            {
                try
                {
                    await _favoriteService.RemoveMediaFromFavoriteAsync(fav.Id, media.Id);
                    Items.Remove(media);
                    var src = fav.Favorite.Medias?.FirstOrDefault(m => m.Id == media.Id);
                    if (src is not null) fav.Favorite.Medias!.Remove(src);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "批量移除中单项失败：FavoriteId={FavoriteId}, MediaId={MediaId}", fav.Id, media.Id);
                }
            }
            fav.NotifyMediaCountChanged();
            ShowEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(SelectedFavoriteCountText));
            Log.Information("批量从收藏夹移除完成：FavoriteId={FavoriteId}, Count={Count}", fav.Id, selected.Count);
        }
        finally
        {
            IsBatchRemoving = false;
            ExitBatchMode();
        }
    }
}
