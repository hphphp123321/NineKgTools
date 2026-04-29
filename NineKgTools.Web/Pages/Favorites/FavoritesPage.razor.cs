using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Media.QueryParameters;

namespace NineKgTools.Pages.Favorites;

public partial class FavoritesPage : ComponentBase
{
    [Inject] FavoriteService FavoriteService { get; set; } = null!;
    [Inject] IDialogService DialogService { get; set; } = null!;
    [Inject] ISnackbar Snackbar { get; set; } = null!;

    private bool _isLoading = true;
    private List<Favorite> _favorites = new();
    private Favorite? _selectedFavorite;
    private string _newFavoriteName = "";

    private bool _createPopoverOpen;
    private bool _renamePopoverOpen;
    private string _renameFavoriteName = "";
    private Favorite? _favoriteToRename;
    private bool _isSubmitting;

    private MediaShownView? _mediaShownView;
    private MediaQueryParameters _initialParams = new();
    
    protected override async Task OnInitializedAsync()
    {
        await LoadFavorites();
    }
    
    // 创建收藏夹相关方法
    private void ToggleCreateFavoritePopover()
    {
        _createPopoverOpen = !_createPopoverOpen;
        if (_createPopoverOpen)
        {
            _newFavoriteName = "";
        }
    }
    
    private async Task CreateFavorite()
    {
        if (string.IsNullOrWhiteSpace(_newFavoriteName) || _isSubmitting)
            return;

        _isSubmitting = true;
        try
        {
            var name = _newFavoriteName.Trim();
            var newFavorite = new Favorite { Name = name };
            await FavoriteService.AddFavoriteAsync(newFavorite);
            Snackbar.Add($"成功创建收藏夹: {name}", Severity.Success);
            _createPopoverOpen = false;
            _newFavoriteName = "";
            await LoadFavorites(showLoading: false);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"创建收藏夹失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
    
    // 重命名收藏夹相关方法
    private void ToggleRenameFavoritePopover(Favorite favorite)
    {
        if (favorite.Id == StaticFavorites.DefaultFavorite.Id)
        {
            Snackbar.Add("默认收藏夹不能重命名", Severity.Warning);
            return;
        }
        
        _favoriteToRename = favorite;
        _renameFavoriteName = favorite.Name;
        _renamePopoverOpen = !_renamePopoverOpen;
    }
    
    private void CloseRenamePopover()
    {
        _renamePopoverOpen = false;
        _favoriteToRename = null;
    }
    
    private async Task RenameFavorite(Favorite favorite, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || _isSubmitting)
            return;

        _isSubmitting = true;
        try
        {
            favorite.Name = newName.Trim();
            await FavoriteService.UpdateFavoriteAsync(favorite);
            Snackbar.Add("收藏夹重命名成功", Severity.Success);
            _renamePopoverOpen = false;
            _favoriteToRename = null;
            await LoadFavorites(showLoading: false);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"重命名收藏夹失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
    
    private async Task LoadFavorites(bool showLoading = true)
    {
        if (showLoading)
        {
            _isLoading = true;
            StateHasChanged();
        }

        try
        {
            _favorites = await FavoriteService.GetAllFavoritesAsync();

            // 如果之前有选中的收藏夹，重新选择它
            if (_selectedFavorite != null)
            {
                var favorite = _favorites.FirstOrDefault(f => f.Id == _selectedFavorite.Id);
                if (favorite != null)
                {
                    _selectedFavorite = favorite;
                }
                else
                {
                    _selectedFavorite = null;
                }
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载收藏夹失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
    
    private void SelectFavorite(Favorite favorite)
    {
        _selectedFavorite = favorite;
        _initialParams = new MediaQueryParameters
        {
            FavoriteNames = new List<string> { favorite.Name }
        };
        StateHasChanged();
    }
    
    private async Task OpenDeleteFavoriteDialog(Favorite favorite)
    {
        if (favorite.Id == StaticFavorites.DefaultFavorite.Id)
        {
            Snackbar.Add("默认收藏夹不能删除", Severity.Warning);
            return;
        }
        
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "删除收藏夹",
            "收藏夹中所有媒体的关联将被解除，媒体本身不会被删除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: favorite.Name,
            targetIcon: Icons.Material.Filled.Bookmark);

        if (confirmed)
        {
            await DeleteFavorite(favorite);
        }
    }
    
    private async Task DeleteFavorite(Favorite favorite)
    {
        if (_isSubmitting) return;

        _isSubmitting = true;
        try
        {
            await FavoriteService.RemoveFavoriteAsync(favorite.Id);
            Snackbar.Add("收藏夹删除成功", Severity.Success);

            if (_selectedFavorite?.Id == favorite.Id)
            {
                _selectedFavorite = null;
            }

            await LoadFavorites(showLoading: false);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"删除收藏夹失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }
} 