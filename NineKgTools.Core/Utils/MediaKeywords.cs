namespace NineKgTools.Utils;

/// <summary>
/// 媒体关键词结构，用于存储从文件名中提取的各种关键信息
/// </summary>
public class MediaKeywords
{
    /// <summary>
    /// 主关键词（通常是作品名的核心部分）
    /// </summary>
    public string PrimaryKeyword { get; set; } = string.Empty;

    /// <summary>
    /// 次要关键词列表（可能包含副标题、版本信息等）
    /// </summary>
    public List<string> SecondaryKeywords { get; set; } = new();

    /// <summary>
    /// 产品代码（如 RJ12345, BJ12345, VJ12345 等）
    /// </summary>
    public string? ProductCode { get; set; }

    /// <summary>
    /// 社团名/出版社名（通常在方括号中）
    /// </summary>
    public string? CircleName { get; set; }

    /// <summary>
    /// 清理后的完整标题
    /// </summary>
    public string CleanedTitle { get; set; } = string.Empty;

    /// <summary>
    /// 检测到的主要语言
    /// </summary>
    public Language DetectedLanguage { get; set; } = Language.Unknown;

    /// <summary>
    /// 版本信息（如 v1.0, v2.1 等）
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 日期信息（如果文件名中包含日期）
    /// </summary>
    public string? Date { get; set; }

    /// <summary>
    /// 获取用于搜索的最佳关键词
    /// </summary>
    public string GetBestSearchKeyword()
    {
        // 如果有产品代码，优先使用
        if (!string.IsNullOrEmpty(ProductCode))
            return ProductCode;

        // 否则使用主关键词
        return PrimaryKeyword;
    }

    /// <summary>
    /// 获取所有关键词（主要+次要）
    /// </summary>
    public List<string> GetAllKeywords()
    {
        var allKeywords = new List<string>();
        
        if (!string.IsNullOrEmpty(PrimaryKeyword))
            allKeywords.Add(PrimaryKeyword);
            
        allKeywords.AddRange(SecondaryKeywords);
        
        if (!string.IsNullOrEmpty(CircleName))
            allKeywords.Add(CircleName);
            
        return allKeywords.Distinct().ToList();
    }
}

/// <summary>
/// 语言枚举
/// </summary>
public enum Language
{
    Unknown,
    Chinese,
    Japanese,
    English
}