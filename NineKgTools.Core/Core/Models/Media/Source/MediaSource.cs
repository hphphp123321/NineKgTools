using NineKgTools.Core.Models.Categories;
using Serilog;

namespace NineKgTools.Core.Models.Media.Source;

/// <summary>
/// 媒体源，包含了媒体的文件路径等信息
/// </summary>
public class MediaSource
{
    /// <summary>
    /// 自增主键 ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 文件或者文件夹的全路径
    /// </summary>
    public string FullPath { get; set; } = "";

    /// <summary>
    /// 是否是文件夹
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    /// 可能的顶层分类，决定了媒体的类型是视频、音频、图片、文本还是游戏
    /// </summary>
    public TopCategory PossibleTopCategory { get; set; }

    /// <summary>
    /// 入口文件路径（游戏的exe、视频的mp4等可直接打开的文件）
    /// </summary>
    public string? EntryFilePath { get; set; }

    /// <summary>
    /// 是否已执行过识别流程（无论结果是否入库）
    /// </summary>
    public bool Identified { get; set; } = false;

    /// <summary>
    /// 识别结果是否已作为 MediaBase 写入数据库
    /// </summary>
    public bool InDatabase { get; set; } = false;

    /// <summary>
    /// 关联的媒体
    /// </summary>
    public MediaBase? MediaBase { get; set; }

    /// <summary>
    /// 无参数构造函数（EF Core 需要）
    /// </summary>
    public MediaSource()
    {
    }

    /// <summary>
    /// 根据路径创建媒体源，自动判断是文件还是文件夹
    /// </summary>
    public MediaSource(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;

        FullPath = Path.GetFullPath(path);
        IsFolder = Directory.Exists(FullPath);
        PossibleTopCategory = IsFolder ? GetFolderTopCategory() : MediaSourceExtensions.GetMediaTopCategory(path);

        // 单文件时自动设置入口文件
        if (!IsFolder)
            EntryFilePath = FullPath;
    }

    /// <summary>
    /// 获取文件夹的顶层分类，依照优先级从高到低：游戏、视频、音频、图片、文本
    /// </summary>
    private TopCategory GetFolderTopCategory()
    {
        var countCache = new Dictionary<TopCategory, int>();

        try
        {
            var directoryInfo = new DirectoryInfo(FullPath);
            var files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var topCategory = MediaSourceExtensions.GetMediaTopCategory(file.FullName);
                countCache.TryAdd(topCategory, 0);
                countCache[topCategory]++;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "计算文件数量失败：{FullPath}", FullPath);
        }

        // 按优先级返回分类
        if (countCache.TryGetValue(TopCategory.Game, out var gameCount) && gameCount > 0)
            return TopCategory.Game;

        if (countCache.TryGetValue(TopCategory.Video, out var videoCount) && videoCount > 0)
            return TopCategory.Video;

        if (countCache.TryGetValue(TopCategory.Audio, out var audioCount) && audioCount > 0)
            return TopCategory.Audio;

        if (countCache.TryGetValue(TopCategory.Picture, out var pictureCount) && pictureCount > 0)
            return TopCategory.Picture;

        if (countCache.TryGetValue(TopCategory.Text, out var textCount) && textCount > 0)
            return TopCategory.Text;

        return TopCategory.Unknown;
    }

    public MediaSource Copy()
    {
        return new MediaSource
        {
            Id = Id,
            FullPath = FullPath,
            IsFolder = IsFolder,
            PossibleTopCategory = PossibleTopCategory,
            EntryFilePath = EntryFilePath,
            Identified = Identified,
            InDatabase = InDatabase
        };
    }

    /// <summary>
    /// 获取文件或者文件夹的大小
    /// </summary>
    /// <returns>文件/夹大小，单位Bytes</returns>
    public long GetSize()
    {
        try
        {
            if (!IsFolder)
            {
                var fileInfo = new FileInfo(FullPath);
                return fileInfo.Length;
            }

            var directoryInfo = new DirectoryInfo(FullPath);
            return directoryInfo.GetFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
        }
        catch (Exception e)
        {
            Log.Error(e, "获取文件大小失败：{FullPath}", FullPath);
            return 0;
        }
    }

    /// <summary>
    /// 获取文件数量
    /// </summary>
    /// <param name="extension">特定后缀名</param>
    public int GetFileCount(string extension = "")
    {
        try
        {
            if (!IsFolder)
                return 1;

            var directoryInfo = new DirectoryInfo(FullPath);
            return directoryInfo.GetFiles("*" + extension, SearchOption.AllDirectories).Length;
        }
        catch (Exception e)
        {
            Log.Error(e, "获取文件数量失败：{FullPath}", FullPath);
            return 0;
        }
    }

    /// <summary>
    /// 获取文件数量
    /// </summary>
    /// <param name="extensions">复数后缀名</param>
    public int GetFileCount(IEnumerable<string> extensions)
    {
        try
        {
            if (!IsFolder)
                return 1;

            var directoryInfo = new DirectoryInfo(FullPath);
            return directoryInfo.GetFiles("*", SearchOption.AllDirectories)
                .Count(file => extensions.Contains(file.Extension.ToLower()));
        }
        catch (Exception e)
        {
            Log.Error(e, "获取文件数量失败：{FullPath}", FullPath);
            return 0;
        }
    }

    /// <summary>
    /// 获取文件名
    /// </summary>
    public string GetFileName()
    {
        return Path.GetFileName(FullPath);
    }

    public void Update(MediaSource target)
    {
        FullPath = target.FullPath;
        IsFolder = target.IsFolder;
        PossibleTopCategory = target.PossibleTopCategory;
        EntryFilePath = target.EntryFilePath;
        Identified = target.Identified;
        InDatabase = target.InDatabase;
    }

    /// <summary>
    /// 获取媒体源详情页链接
    /// </summary>
    public string GetSourceLink() => $"/source/{Id}";
}

public static class MediaSourceExtensions
{
    public static readonly List<string> VideoExtensions;
    public static readonly List<string> AudioExtensions;
    public static readonly List<string> PictureExtensions;
    public static readonly List<string> TextExtensions;
    public static readonly List<string> GameExtensions;

    static MediaSourceExtensions()
    {
        VideoExtensions = TopCategoryExtensions.Extensions[TopCategory.Video];
        AudioExtensions = TopCategoryExtensions.Extensions[TopCategory.Audio];
        PictureExtensions = TopCategoryExtensions.Extensions[TopCategory.Picture];
        TextExtensions = TopCategoryExtensions.Extensions[TopCategory.Text];
        GameExtensions = TopCategoryExtensions.Extensions[TopCategory.Game];
    }

    /// <summary>
    /// 根据媒体后缀名获取顶层分类
    /// </summary>
    /// <param name="mediaPath">媒体的路径</param>
    /// <returns>媒体的顶层分类</returns>
    public static TopCategory GetMediaTopCategory(string mediaPath)
    {
        string extension = Path.GetExtension(mediaPath).ToLower();

        if (VideoExtensions.Contains(extension))
            return TopCategory.Video;

        if (AudioExtensions.Contains(extension))
            return TopCategory.Audio;

        if (PictureExtensions.Contains(extension))
            return TopCategory.Picture;

        if (TextExtensions.Contains(extension))
            return TopCategory.Text;

        if (GameExtensions.Contains(extension))
            return TopCategory.Game;

        return TopCategory.Unknown;
    }
}
