using System.Text.RegularExpressions;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media.Source;
using Serilog;

namespace NineKgTools.Core.Services.Websites.DLsite;

/// <summary>
/// DLsite工具类，包含辅助方法
/// </summary>
public static partial class DLsiteUtils
{
    private const string ManiaxCode = "RJ";
    private const string BooksCode = "BJ";
    private const string ProCode = "VJ";

    /// <summary>
    /// 通过DLsite资源代码（例如RJXXXX）获取URL
    /// </summary>
    /// <param name="dlsiteCode">RJXXX, BJXXXX, VJXXXX</param>
    /// <returns>资源网站，如果无法识别则返回null</returns>
    public static string? GetUrlByDLsiteCode(string dlsiteCode)
    {
        // 同人游戏
        if (dlsiteCode.Contains(ManiaxCode)) // RJ01081508-游戏，RJ01205539-视频
        {
            return $"https://www.dlsite.com/maniax/work/=/product_id/{dlsiteCode}.html?locale=zh-CN";
        }

        if (dlsiteCode.Contains(BooksCode)) // BJ566243-漫画
        {
            return $"https://www.dlsite.com/books/work/=/product_id/{dlsiteCode}.html?locale=zh-CN";
        }

        // 美少女游戏
        if (dlsiteCode.Contains(ProCode)) // VJ014316-美少女游戏
        {
            return $"https://www.dlsite.com/pro/work/=/product_id/{dlsiteCode}.html?locale=zh-CN";
        }

        Log.Warning("无法识别的DLsite代码: {DLsiteCode}", dlsiteCode);
        return null;
    }

    /// <summary>
    /// 用正则判断名字内是否含有DLsite代码
    /// </summary>
    public static string? TryGetDLsiteCodeByName(string name)
    {
        var match = DLsiteRegex().Match(name);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// 添加URL语言后缀
    /// </summary>
    public static void AddUrlLocaleSuffix(ref string url, string locale)
    {
        // 用正则判断后缀是否已经存在
        if (url.Contains("?locale="))
        {
            // 如果已经存在，替换成现有的locale
            url = Regex.Replace(url, @"locale=[a-zA-Z-]+", $"locale={locale}");
        }
        else
        {
            url += $"?locale={locale}";
        }
    }

    [GeneratedRegex(@"(RJ|BJ|VJ)\d+")]
    private static partial Regex DLsiteRegex();
}