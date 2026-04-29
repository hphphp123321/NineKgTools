namespace NineKgTools.Core.Models.Categories;

public static class StaticCategories   
{
    public static Dictionary<string, TopCategory> TopCategoryNameMap { get; } = new()
    {
        { "unknown", TopCategory.Unknown },
        { "video", TopCategory.Video },
        { "audio", TopCategory.Audio },
        { "picture", TopCategory.Picture },
        { "text", TopCategory.Text },
        { "game", TopCategory.Game },
    };
    
    public static Category Unknown = new(1, "未知", TopCategory.Unknown);
    
    #region 视频
    public static readonly Category AdultVideo = new Category(201, "AV", TopCategory.Video);
    public static readonly Category HAnime = new Category(202, "H动画", TopCategory.Video);
    public static readonly Category Fc2Video = new Category(203, "FC2视频", TopCategory.Video);
    public static readonly Category OtherVideo = new Category(299, "其他视频", TopCategory.Video);
    #endregion
    
    #region 音频
    public static readonly Category Asmr = new Category(301, "ASMR", TopCategory.Audio);
    public static readonly Category OtherAudio = new Category(399, "其他音频", TopCategory.Audio);
    #endregion
    
    #region 图片
    public static readonly Category Manga = new Category(401, "漫画/本子", TopCategory.Picture);
    public static readonly Category PictureSet = new Category(402, "图片集", TopCategory.Picture);
    public static readonly Category OtherPicture = new Category(499, "其他图片", TopCategory.Picture);
    #endregion

    #region 文本
    public static readonly Category Novel = new Category(501, "小说", TopCategory.Text);
    public static readonly Category OtherText = new Category(599, "其他文本", TopCategory.Text);
    #endregion
    
    #region 游戏
    public static readonly Category RpgGame = new Category(601, "角色扮演（RPG）", TopCategory.Game);
    public static readonly Category ActGame = new Category(602, "动作（ACT/ACN）", TopCategory.Game);
    public static readonly Category AvgGame = new Category(603, "冒险（AVG/ADV）", TopCategory.Game);
    public static readonly Category StgGame = new Category(604, "射击（STG）", TopCategory.Game);
    public static readonly Category SlgGame = new Category(605, "模拟（SLG/SLN）", TopCategory.Game);
    public static readonly Category OtherGame = new Category(699, "其他游戏", TopCategory.Game);
    #endregion
    
    public static List<Category> CategoryList { get; } = new()
    {
        Unknown,
        AdultVideo,
        HAnime,
        Fc2Video,
        OtherVideo,
        Asmr,
        OtherAudio,
        Manga,
        PictureSet,
        OtherPicture,
        Novel,
        OtherText,
        RpgGame,
        ActGame,
        AvgGame,
        StgGame,
        SlgGame,
        OtherGame,
    };
}