using System.Text.RegularExpressions;

namespace NineKgTools.Utils;

public static partial class FileSizeFormatter
{
    /// <summary>
    /// 格式化文件大小
    /// </summary>
    /// <param name="bytes">字节量</param>
    /// <returns>格式化后的字符串</returns>
    public static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
    
    /// <summary>
    /// 通过正则解析文件大小
    /// </summary>
    /// <param name="size">文件大小字符串，例如145MB, 3 GB等等</param>
    /// <returns>文件大小，以Byte为单位</returns>
    public static long ParseFileSize(string size)
    {
        var match = FileSizeRegex().Match(size);
        if (!match.Success)
        {
            return 0;
        }
        var value = double.Parse(match.Groups[1].Value);
        var unit = match.Groups[2].Value;
        switch (unit)
        {
            case "B":
                return (long)value;
            case "KB":
                return (long)(value * 1024);
            case "MB":
                return (long)(value * 1024 * 1024);
            case "GB":
                return (long)(value * 1024 * 1024 * 1024);
            case "TB":
                return (long)(value * 1024 * 1024 * 1024 * 1024);
            default:
                return 0;
        }
    }

    [GeneratedRegex(@"(\d+\.?\d*)\s*(\w+)")]
    private static partial Regex FileSizeRegex();
}