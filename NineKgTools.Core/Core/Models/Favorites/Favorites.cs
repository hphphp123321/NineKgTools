namespace NineKgTools.Core.Models.Favorites;

public static class StaticFavorites
{
    public static Favorite DefaultFavorite { get; } = new() { Id = 1, Name = "默认收藏夹" };

    public static List<Favorite> Copy(this List<Favorite> favorites)
    {
        return favorites.Select(favorite => favorite.Copy()).ToList();
    }
    
}