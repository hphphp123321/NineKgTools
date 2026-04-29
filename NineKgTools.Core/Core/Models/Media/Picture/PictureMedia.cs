namespace NineKgTools.Core.Models.Media.Picture;

/// <summary>
/// 图片媒体分类
/// </summary>
public class PictureMedia : MediaBase
{
    /// <summary>
    /// 图片页数
    /// </summary>
    public int PageNum { get; set; }

    /// <summary>
    /// 画师
    /// </summary>
    public List<Creator>? Illustrators { get; set; } = new();

    /// <summary>
    /// 演员
    /// </summary>
    public List<Creator>? Actors { get; set; } = new();

    /// <summary>
    /// 作者
    /// </summary>
    public List<Creator>? Authors { get; set; } = new();

    public PictureMedia()
    {
    }

    public PictureMedia(MediaBase media) : base(media)
    {
    }
}
