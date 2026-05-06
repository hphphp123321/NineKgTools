using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Favorites;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 收藏夹左侧列表里每一行 VM。承载 Favorite 实体 + 是否默认 / 媒体计数。
/// </summary>
public partial class FavoriteItemViewModel : ObservableObject
{
    public Favorite Favorite { get; }

    public int Id => Favorite.Id;
    public string Name => Favorite.Name;
    public int MediaCount => Favorite.Medias?.Count ?? 0;

    /// <summary>默认收藏夹不可删除 / 不可改名</summary>
    public bool IsDefault => Favorite.Id == StaticFavorites.DefaultFavorite.Id;

    [ObservableProperty]
    private bool _isSelected;

    public FavoriteItemViewModel(Favorite favorite)
    {
        Favorite = favorite;
    }
}
