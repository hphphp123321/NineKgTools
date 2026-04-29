using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Models.Tags;

public class TopTag
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public List<Tag> Tags { get; set; } = new List<Tag>();

    public TopTag Copy()
    {
        return new TopTag
        {
            Id = Id,
            Name = Name,
            Tags = Tags.Select(t => t.Copy()).ToList() // 深拷贝
        };
    }
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    
    public string? Description { get; set; }
    
    public List<MediaBase> Medias { get; set; } = new(); // 多对多关系
    
    public TopTag TopTag { get; set; }

    public Tag Copy()
    {
        return new Tag
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Medias = Medias,
            TopTag = TopTag
        };
    }
    
    public int Count => Medias.Count; // 计算属性，返回该标签下的媒体数量
    
    public Tag() {} // 无参构造函数，用于反序列化
    
    public Tag(int id, string name, string? description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public string GetTagLink()
    {
        return $"/tag/{Id}";
    }
}

public class YamlTags
{
    public List<TopTag> Tags { get; set; }
}