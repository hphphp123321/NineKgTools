namespace NineKgTools.Core.Models.Media;

/// <summary>
/// 社团、开发商、发行商
/// </summary>
public class Circle
{
    public int Id { get; set; }
    public required string Name { get; set; }
    
    /// <summary>
    /// 别名列表
    /// </summary>
    public List<string> AliasNames { get; set; } = new();
    
    public Image? Avatar { get; set; }
    
    public string? Description { get; set; }
    
    public string? DescriptionTranslated { get; set; }
    
    public List<MediaBase> Medias { get; set; } = new();
    
    #region Methods方法

    public Circle Copy()
    {
        return new Circle
        {
            Name = Name,
            AliasNames = new List<string>(AliasNames),
            Avatar = Avatar?.Copy(),
            Description = Description,
            DescriptionTranslated = DescriptionTranslated,
        };
    }

    public string GetCircleLink()
    {
        return $"/circle/{Id}";
    }
    
    public void Update(Circle circle)
    {
        Name = circle.Name;
        AliasNames = circle.AliasNames;
        Description = circle.Description;
        DescriptionTranslated = circle.DescriptionTranslated;
        if (circle.Avatar != null)
        {
            Avatar = circle.Avatar;
        }
    }

    #endregion
    
}