using System.Text.Json.Serialization;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Models.Tags;

namespace NineKgTools.Core.Models.Media;

[JsonDerivedType(typeof(GameMedia), "game")]
[JsonDerivedType(typeof(VideoMedia), "video")]
[JsonDerivedType(typeof(AudioMedia), "audio")]
[JsonDerivedType(typeof(PictureMedia), "picture")]
[JsonDerivedType(typeof(TextMedia), "text")]
public class MediaBase
{
    /// <summary>
    /// 作品的唯一标识
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 媒体源，包含了媒体的文件路径等信息
    /// </summary>
    public MediaSource? Source { get; set; }

    /// <summary>
    /// 分类
    /// </summary>
    public Category Category { get; set; } = StaticCategories.Unknown;

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = "标题";
    
    /// <summary>
    /// 社团、出版社、出版商、企画等等
    /// </summary>
    public Circle? Circle { get; set; }

    /// <summary>
    /// 统一的创作者列表（多对多关系）
    /// 此属性是从子类各个角色属性（Authors、Illustrators等）自动同步而来
    /// 不应手动修改，应修改具体角色属性后调用 SyncCreators()
    /// </summary>
    public List<Creator> Creators { get; set; } = new();

    /// <summary>
    /// 别名
    /// </summary>
    public List<string> AliasTitles { get; set; } = new();

    /// <summary>
    /// 发售日/发行日期/上映日期等等
    /// </summary>
    public DateTime? ReleaseDate { get; set; }

    /// <summary>
    /// 入库日期
    /// </summary>
    public DateTime? StoreDate { get; set; }

    /// <summary>
    /// 最后打开日期
    /// </summary>
    public DateTime? LastOpenDate { get; set; }

    /// <summary>
    /// 简介
    /// </summary>
    public string Summary { get; set; } = "暂无简介";

    /// <summary>
    /// 翻译后的简介
    /// </summary>
    public string? SummaryTranslated { get; set; }

    /// <summary>
    /// 具体描述，在页面上以@Html.Raw()的方式展示
    /// </summary>
    public string Description { get; set; } = "暂无描述";

    /// <summary>
    /// 翻译后的描述
    /// </summary>
    public string? DescriptionTranslated { get; set; }

    /// <summary>
    /// 标签
    /// </summary>
    public List<Tag> Tags { get; set; } = new();

    /// <summary>
    /// 文件大小, 单位字节Byte
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// 收藏夹
    /// </summary>
    public List<Favorite> Favorites { get; set; } = new();

    /// <summary>
    /// 作品的相关链接，可以是官网、各种网站等等
    /// </summary>
    public List<Uri> Links { get; set; } = new();

    /// <summary>
    /// 海报图片，用来展示在作品墙上的图片, TODO：应该每张图片有各种尺寸，small、medium、large、grid（用于海报墙）等等
    /// </summary>
    public Image? Poster { get; set; }

    /// <summary>
    /// 其余用来展示的图片
    /// </summary>
    public List<Image> Pictures { get; set; } = new();

    /// <summary>
    /// 评分，满分5分
    /// </summary>
    public float Rating { get; set; }

    /// <summary>
    /// 关联的媒体
    /// </summary>
    public List<MediaBase> RelatedMedias { get; set; } = new();

    /// <summary>
    /// 一些自定义的信息，比如演员、导演、制作商、发行商等等
    /// </summary>
    public Dictionary<string, string> Infos { get; set; } = new();


    #region Methods方法

    public MediaBase()
    {
    }

    protected MediaBase(MediaBase other)
    {
        Id = other.Id;
        Source = other.Source?.Copy();
        Category = other.Category;
        Title = other.Title;
        Circle = other.Circle?.Copy();
        Creators = other.Creators.ToList();
        AliasTitles = other.AliasTitles;
        ReleaseDate = other.ReleaseDate;
        StoreDate = other.StoreDate;
        LastOpenDate = other.LastOpenDate;
        Summary = other.Summary;
        SummaryTranslated = other.SummaryTranslated;
        Description = other.Description;
        DescriptionTranslated = other.DescriptionTranslated;
        Tags = other.Tags.Copy();
        Size = other.Size;
        Favorites = other.Favorites.Copy();
        Links = other.Links.ToList();
        Poster = other.Poster?.Copy();
        Pictures = other.Pictures.Select(p => p.Copy()).ToList();
        Rating = other.Rating;
        RelatedMedias = other.RelatedMedias;
        Infos = other.Infos;
    }

    public MediaBase Copy()
    {
        return new MediaBase
        {
            Id = Id,
            Source = Source?.Copy(),
            Category = Category,
            Title = Title,
            Circle = Circle?.Copy(),
            Creators = Creators.ToList(), // 复制 Creators 列表
            AliasTitles = AliasTitles,
            ReleaseDate = ReleaseDate,
            StoreDate = StoreDate,
            LastOpenDate = LastOpenDate,
            Summary = Summary,
            SummaryTranslated = SummaryTranslated,
            Description = Description,
            DescriptionTranslated = DescriptionTranslated,
            Tags = Tags.Copy(),
            Size = Size,
            Favorites = Favorites.Copy(),
            Links = Links.ToList(),
            Poster = Poster?.Copy(),
            Pictures = Pictures.Select(picture => picture.Copy()).ToList(),
            Rating = Rating,
            RelatedMedias = RelatedMedias,
            Infos = Infos,
        };
    }

    /// <summary>
    /// 获取媒体的网页链接
    /// </summary>
    public string GetMediaLink()
    {
        return $"/media/{Id}";
    }
    
    /// <summary>
    /// 判断是否在任何收藏夹中
    /// </summary>
    /// <returns></returns>
    public bool IsFavorite => Favorites.Count > 0;

    /// <summary>
    /// 从子类的各个角色属性同步所有 Creator 到统一的 Creators 集合
    /// 此方法应在保存 Media 前自动调用（通过 SaveChanges 拦截器）
    /// </summary>
    public void SyncCreators()
    {
        var allCreators = new HashSet<Creator>();

        // 根据实际类型收集 Creator
        switch (this)
        {
            case Game.GameMedia game:
                AddCreatorsFromLists(allCreators,
                    game.Authors,
                    game.Illustrators,
                    game.Musicians,
                    game.ScreenWriters,
                    game.VoiceActors);
                break;

            case Audio.AudioMedia audio:
                AddCreatorsFromLists(allCreators,
                    audio.Authors,
                    audio.Illustrators,
                    audio.Musicians,
                    audio.ScreenWriters,
                    audio.VoiceActors);
                break;

            case Video.VideoMedia video:
                AddCreatorsFromLists(allCreators,
                    video.ScreenWriters,
                    video.Illustrators,
                    video.Actors,
                    video.Musicians,
                    video.Directors);
                break;

            case Picture.PictureMedia picture:
                AddCreatorsFromLists(allCreators,
                    picture.Illustrators,
                    picture.Actors,
                    picture.Authors);
                break;

            case Text.TextMedia text:
                AddCreatorsFromLists(allCreators, text.Illustrators);
                // TextMedia 的 Author 是单个对象，不是列表
                if (text.Author != null)
                {
                    allCreators.Add(text.Author);
                }
                break;
        }

        // 智能更新 Creators 集合：只做必要的增删操作
        // 找出需要移除的（在 Creators 中但不在 allCreators 中）
        var toRemove = Creators.Where(c => !allCreators.Any(ac => ac.Id == c.Id)).ToList();
        // 找出需要添加的（在 allCreators 中但不在 Creators 中）
        var toAdd = allCreators.Where(ac => !Creators.Any(c => c.Id == ac.Id)).ToList();

        foreach (var creator in toRemove)
        {
            Creators.Remove(creator);
        }

        foreach (var creator in toAdd)
        {
            Creators.Add(creator);
        }
    }

    /// <summary>
    /// 从多个 Creator 列表中添加到 HashSet（自动去重）
    /// </summary>
    private static void AddCreatorsFromLists(HashSet<Creator> targetSet, params List<Creator>?[] creatorLists)
    {
        foreach (var list in creatorLists)
        {
            if (list != null)
            {
                foreach (var creator in list)
                {
                    targetSet.Add(creator);
                }
            }
        }
    }

    #endregion
}