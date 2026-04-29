namespace NineKgTools.Core.Models.Categories;

public static class TopCategoryExtensions
{
    public static List<string> GetExtensions(TopCategory topCategory)
    {
        return Extensions[topCategory];
    }
    public static Dictionary<TopCategory, List<string>> Extensions { get; } = new()
    {
        { TopCategory.Unknown, new List<string>() },

        // .mp4, .mkv, .avi, .wmv, .mov, .flv, .m4v, .mpeg, .mpg, .rmvb, .vob, .3gp, .divx, .webm
        {
            TopCategory.Video,
            new List<string>()
            {
                ".mp4", ".mkv", ".avi", ".wmv", ".mov", ".flv", ".m4v", ".mpeg", ".mpg", ".rmvb", ".vob", ".3gp",
                ".divx", ".webm"
            }
        },

        // .mp3, .wav, .aac, .flac, .ogg, .wma, .m4a, .alac, .ape
        { TopCategory.Audio, new List<string>()
        {
            ".mp3", ".wav", ".aac", ".flac", ".ogg", ".wma", ".m4a", ".alac", ".ape"
        } },

        // .jpg, .jpeg, .png, .gif, .bmp, .tiff, .svg, .webp, .ico, .psd
        { TopCategory.Picture, new List<string>()
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".svg", ".webp", ".ico", ".psd"
        } },

        // .txt, .pdf, .doc, .docx, .epub, .mobi, .rtf, .html, .htm, .md
        { TopCategory.Text, new List<string>()
        {
            ".txt", ".pdf", ".doc", ".docx", ".epub", ".mobi", ".rtf", ".html", ".htm", ".md"
        } },
        
        
        // .exe, .app, .apk, .ipa, .nes, .smc, .iso, .bin, .gba, .jar
        { TopCategory.Game, new List<string>()
        {
            ".exe", ".app", ".apk", ".ipa", ".nes", ".smc", ".iso", ".bin", ".gba", ".jar"
        } },
    };

    public static string GetCnName(this TopCategory topCategory)
    {
        return topCategory switch
        {
            TopCategory.Video => "视频",
            TopCategory.Audio => "音频",
            TopCategory.Picture => "图片",
            TopCategory.Text => "文字",
            TopCategory.Game => "游戏",
            _ => "未知"
        };
    }
}