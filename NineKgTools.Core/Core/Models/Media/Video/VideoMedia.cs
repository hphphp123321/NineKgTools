namespace NineKgTools.Core.Models.Media.Video;

// TODO 细化分类：分镜、演出、人物原案、人物设计、作画监督、剪辑、主题曲相关、录音、音响、制片人、动画制作公司、音乐制作公司
// TODO 还有人物分开，类似bangumi的角色介绍
public class VideoMedia : MediaBase
{
    /// <summary>
    /// 集数、话数
    /// </summary>
    public int Episodes { get; set; }

    /// <summary>
    /// 制作公司、动画制作公司
    /// </summary>
    public List<Circle>? Makers { get; set; } = new();

    /// <summary>
    /// 编剧、脚本、监督等相关人员
    /// </summary>
    public List<Creator>? ScreenWriters { get; set; } = new();

    /// <summary>
    /// 原画、人物设计、原案、作画监督等相关人员
    /// </summary>
    public List<Creator>? Illustrators { get; set; } = new();

    /// <summary>
    /// 演员（包括声优）
    /// </summary>
    public List<Creator>? Actors { get; set; } = new();

    /// <summary>
    /// 音乐相关人员
    /// </summary>
    public List<Creator>? Musicians { get; set; } = new();

    /// <summary>
    /// 导演、演出、分镜、剪辑等相关人员
    /// </summary>
    public List<Creator>? Directors { get; set; } = new();

    public VideoMedia()
    {
    }

    public VideoMedia(MediaBase media) : base(media)
    {
    }
}
