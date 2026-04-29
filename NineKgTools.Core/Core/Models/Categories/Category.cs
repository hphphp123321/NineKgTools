namespace NineKgTools.Core.Models.Categories;

public enum TopCategory
{
    Unknown = 1,
    Video = 2,
    Audio = 3,
    Picture = 4,
    Text = 5,
    Game = 6,
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public TopCategory TopCategory { get; set; }

    public Category()
    {
        
    }
    public Category(int id, string name, TopCategory topCategory)
    {
        Id = id;
        Name = name;
        TopCategory = topCategory;
    }
    
}


