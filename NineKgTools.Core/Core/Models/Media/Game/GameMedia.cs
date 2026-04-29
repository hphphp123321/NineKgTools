namespace NineKgTools.Core.Models.Media.Game;

/// <summary>
/// 游戏媒体分类
/// </summary>
public class GameMedia : MediaBase
{
    /// <summary>
    /// 游戏平台
    /// </summary>
    public List<Platform> Platforms { get; set; } = [];

    /// <summary>
    /// 编剧
    /// </summary>
    public List<Creator>? ScreenWriters { get; set; } = [];

    /// <summary>
    /// 插画师
    /// </summary>
    public List<Creator>? Illustrators { get; set; } = [];

    /// <summary>
    /// 声优
    /// </summary>
    public List<Creator>? VoiceActors { get; set; } = [];

    /// <summary>
    /// 音乐
    /// </summary>
    public List<Creator>? Musicians { get; set; } = [];

    /// <summary>
    /// 作者
    /// </summary>
    public List<Creator>? Authors { get; set; } = [];

    public GameMedia()
    {
    }

    public GameMedia(MediaBase media) : base(media)
    {
    }
}
