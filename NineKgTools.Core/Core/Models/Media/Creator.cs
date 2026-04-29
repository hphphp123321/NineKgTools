namespace NineKgTools.Core.Models.Media;

/// <summary>
/// 相关人员，例如声优、音乐、画师等
/// </summary>
public class Creator
{
    public int Id { get; set; }
    public required string Name { get; set; }

    public Image? Avatar { get; set; }

    public required List<CreatorType> Types { get; set; }

    /// <summary>
    /// 别名列表
    /// </summary>
    public List<string> AliasNames { get; set; } = new();

    public string? Description { get; set; }

    public string? DescriptionTranslated { get; set; }

    /// <summary>
    /// 反向导航属性：该创作者关联的所有媒体
    /// </summary>
    public List<MediaBase> Medias { get; set; } = new();

    public void Update(Creator creator)
    {
        Name = creator.Name;
        Avatar = creator.Avatar;
        Types = Types.Union(creator.Types).ToList(); // 合并Types列表并去重
        AliasNames = creator.AliasNames;
        Description = creator.Description;
        DescriptionTranslated = creator.DescriptionTranslated;
    }

    public string GetCreatorLink()
    {
        return $"/creator/{Id}";
    }
}

public enum CreatorType
{
    Author, // 作者
    Illustrator, // 画师
    Musician, // 音乐
    ScreenWriter, // 编剧
    VoiceActor, // 声优
    Director, // 导演
    Actor, // 演员
}