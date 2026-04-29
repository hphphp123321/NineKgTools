using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Models.Favorites;

public class Favorite
{
    public int Id { get; set; }
    public required string Name { get; set; }
    
    public List<MediaBase> Medias { get; set; } = new();

    public Favorite Copy()
    {
        return new Favorite
        {
            Id = Id,
            Name = Name
        };
    }
}