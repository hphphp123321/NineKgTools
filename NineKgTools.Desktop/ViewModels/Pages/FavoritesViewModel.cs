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
        try
        {
            await _favoriteService.RemoveMediaFromFavoriteAsync(fav.Id, media.Id);
            // UI 立即移除（避免完整重新拉媒体列表）
            Items.Remove(media);
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
}
