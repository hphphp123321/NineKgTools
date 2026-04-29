namespace NineKgTools.Core.Models.Media.Text;

public class TextMedia : MediaBase
{
    /// <summary>
    /// 文本字数
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// 书本数量
    /// </summary>
    public int BookNum { get; set; }


    /// <summary>
    /// 插画师
    /// </summary>
    public List<Creator>? Illustrators { get; set; } = [];

    /// <summary>
    /// 作者
    /// </summary>
    public Creator? Author { get; set; }

    public TextMedia()
    {
    }

    public TextMedia(MediaBase media) : base(media)
    {
    }
}
