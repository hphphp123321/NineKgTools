namespace NineKgTools.Core.Models.Media.Audio;

public class AudioMedia : MediaBase
{
    /// <summary>
    /// 编剧
    /// </summary>
    public List<Creator>? ScreenWriters { get; set; } = new();

    /// <summary>
    /// 插画师
    /// </summary>
    public List<Creator>? Illustrators { get; set; } = new();

    /// <summary>
    /// 声优
    /// </summary>
    public List<Creator>? VoiceActors { get; set; } = new();

    /// <summary>
    /// 音乐
    /// </summary>
    public List<Creator>? Musicians { get; set; } = new();

    /// <summary>
    /// 作者
    /// </summary>
    public List<Creator>? Authors { get; set; } = new();

    public AudioMedia()
    {
    }

    public AudioMedia(MediaBase media) : base(media)
    {
    }
}
