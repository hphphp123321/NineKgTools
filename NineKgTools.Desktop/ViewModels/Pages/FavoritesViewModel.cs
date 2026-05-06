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
        // Phase 2.6 MVP：用 confirm dialog 当输入框（简化实现）。Phase 后续可换 Avalonia 输入对话框
        // 暂时简化为：自动生成"新建收藏夹 N"名字，用户后续重命名
        var newName = $"新建收藏夹 {DateTime.Now:HHmmss}";
        try
        {
            await _favoriteService.AddFavoriteAsync(new Favorite { Name = newName });
            await ReloadFavoritesAsync();
            // 选中新建的收藏夹
            var newOne = Favorites.LastOrDefault();
            if (newOne is not null) await SelectFavoriteAsync(newOne);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "新建收藏夹失败");
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
