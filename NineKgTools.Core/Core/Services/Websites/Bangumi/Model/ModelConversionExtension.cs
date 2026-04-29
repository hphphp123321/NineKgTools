using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public static class ModelConversionExtension
{
    /// <summary>
    /// 将 Bangumi 的图片转换为项目图片 TODO 本地图片的存储方式分为small、...
    /// </summary>
    /// <param name="bangumiImage"></param>
    /// <returns></returns>
    public static Image? ToImage(this BangumiImage? bangumiImage)
    {
        if (bangumiImage == null) return null;
        // 依次判断Common、Large、Medium、Grid、Small是否为空，不为空则返回
        if (!string.IsNullOrEmpty(bangumiImage.Common)) return new Image { Url = new Uri(bangumiImage.Common) };
        if (!string.IsNullOrEmpty(bangumiImage.Large)) return new Image { Url = new Uri(bangumiImage.Large) };
        if (!string.IsNullOrEmpty(bangumiImage.Medium)) return new Image { Url = new Uri(bangumiImage.Medium) };
        if (!string.IsNullOrEmpty(bangumiImage.Grid)) return new Image { Url = new Uri(bangumiImage.Grid) };
        if (!string.IsNullOrEmpty(bangumiImage.Small)) return new Image { Url = new Uri(bangumiImage.Small) };
        return null;
    }

    public static Circle ToCircle(this PersonDetail person)
    {
        return new Circle
        {
            Name = person.Name,
            Avatar = person.Images.ToImage(),
            Description = person.Summary
        };
    }

    public static Creator ToCreator(this PersonDetail person)
    {
        var creatorType = person.Relation switch
        {
            "原画" or "插画" or "人物原案" or "人物设计" or "人物设定" or "分镜" or "第二原画" or "作画监督" or "色彩设计" or "色彩指定" or "背景美术"
                or "补间动画" => CreatorType.Illustrator,
            "剧本" => CreatorType.ScreenWriter,
            "音乐" or "音响" or "录音" => CreatorType.Musician,
            "导演" or "剪辑" => CreatorType.Director,
            "编剧" or "脚本" or "原作" => CreatorType.ScreenWriter,
            "声优" => CreatorType.VoiceActor,
            "演员" => CreatorType.Actor,
            "作者" => CreatorType.Author,
            _ => CreatorType.Author // 默认设置为作者
        };

        return new Creator
        {
            Name = person.Name,
            Avatar = person.Images.ToImage(),
            Description = person.Summary,
            Types = new List<CreatorType> { creatorType }
        };
    }

    public static Category ToCategory(this SubjectType type)
    {
        return type switch
        {
            SubjectType.Book => StaticCategories.Novel,
            SubjectType.Animation => StaticCategories.HAnime,
            SubjectType.Music => StaticCategories.OtherAudio, // 后续细分
            SubjectType.Game => StaticCategories.OtherGame, // 后续细分
            SubjectType.Real => StaticCategories.Unknown, // 三次元先分为未知
            _ => StaticCategories.Unknown
        };
    }

    public static MediaSearchResult ToMediaSearchResult(this BangumiSearchResultInstance instance, string searchKey)
    {
        return new MediaSearchResult
        {
            SearchKey = searchKey,
            Title = instance.Name,
            Url = instance.Url,
            Id = Convert.ToString(instance.Id),
            Category = instance.Type.ToCategory(),
            Poster = instance.Images.ToImage()
        };
    }
}