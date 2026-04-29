using System.Text;
using System.Text.RegularExpressions;
using JiebaNet.Segmenter;
using NMeCab.Specialized;

namespace NineKgTools.Utils;

/// <summary>
/// 用于分割媒体名称，分割关键字便于搜索
/// </summary>
public static partial class MediaNameSplitter
{
    private static readonly Lazy<JiebaSegmenter?> ChineseSegmenter = new(() =>
    {
        try
        {
            return new JiebaSegmenter();
        }
        catch
        {
            // 如果初始化失败，返回null
            return null;
        }
    });
    
    /// <summary>
    /// 保留旧接口的兼容性
    /// </summary>
    public static string SplitKeyword(string name)
    {
        var keywords = ExtractKeywords(name);
        return keywords.GetBestSearchKeyword();
    }
    
    /// <summary>
    /// 从文件名中提取结构化的关键词信息
    /// </summary>
    public static MediaKeywords ExtractKeywords(string fileName)
    {
        var result = new MediaKeywords();
        
        // 1. 预处理：智能移除文件扩展名
        // 只有当文件名包含常见的文件扩展名时才移除
        var name = fileName;
        var commonExtensions = new[] { ".mp4", ".mkv", ".avi", ".mp3", ".wav", ".zip", ".rar", ".7z", ".txt", ".pdf", ".epub", ".mobi", ".jpg", ".png", ".gif", ".bmp" };
        var hasKnownExtension = false;
        foreach (var ext in commonExtensions)
        {
            if (fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                hasKnownExtension = true;
                break;
            }
        }
        
        if (hasKnownExtension)
        {
            name = Path.GetFileNameWithoutExtension(fileName) ?? fileName;
        }
        
        // 2. 提取产品代码
        result.ProductCode = ExtractProductCode(name);
        
        // 3. 提取日期
        result.Date = ExtractDate(name);
        
        // 4. 提取版本号
        result.Version = ExtractVersion(name);
        
        // 5. 提取社团名（通常在方括号中）
        result.CircleName = ExtractCircleName(name);
        
        // 6. 清理标题（移除已提取的信息）
        var cleanedName = CleanTitle(name, result);
        result.CleanedTitle = cleanedName;
        
        // 7. 检测语言
        result.DetectedLanguage = DetectLanguage(cleanedName);
        
        // 8. 根据语言进行分词
        var keywords = SegmentByLanguage(cleanedName, result.DetectedLanguage);
        
        // 9. 设置主要和次要关键词
        if (keywords.Count > 0)
        {
            result.PrimaryKeyword = keywords[0];
            if (keywords.Count > 1)
            {
                result.SecondaryKeywords = keywords.Skip(1).ToList();
            }
        }
        else
        {
            // 如果分词失败，使用原始清理后的标题
            result.PrimaryKeyword = cleanedName;
        }
        
        // 10. 处理过长的关键词
        if (result.PrimaryKeyword.Length > 50)
        {
            result.PrimaryKeyword = result.PrimaryKeyword[..50];
        }
        
        return result;
    }
    
    /// <summary>
    /// 提取产品代码（RJ/BJ/VJ等）
    /// </summary>
    private static string? ExtractProductCode(string name)
    {
        var match = ProductCodeRegex().Match(name);
        return match.Success ? match.Value : null;
    }
    
    /// <summary>
    /// 提取日期（通常格式为 [20240101] 或 (2024-01-01)）
    /// </summary>
    private static string? ExtractDate(string name)
    {
        var match = DateRegex().Match(name);
        return match.Success ? match.Value : null;
    }
    
    /// <summary>
    /// 提取版本号
    /// </summary>
    private static string? ExtractVersion(string name)
    {
        var match = VersionRegex().Match(name);
        return match.Success ? match.Value : null;
    }
    
    /// <summary>
    /// 提取社团名（通常在方括号或圆括号中）
    /// </summary>
    private static string? ExtractCircleName(string name)
    {
        // 尝试所有方括号内容，跳过日期
        var matches = SquareBracketRegex().Matches(name);
        foreach (Match match in matches)
        {
            var content = match.Groups[1].Value;
            // 排除日期格式和其他特殊格式
            if (!Regex.IsMatch(content, @"^\d{8}$|^\d{4}-\d{2}-\d{2}$") &&
                !Regex.IsMatch(content, @"^(WEB-DL|1080p|720p|BDRip|MP4|MKV|AVC|AAC|CHT|CHS|ENG|JAP)$", RegexOptions.IgnoreCase) &&
                content.Length > 1)
            {
                return content;
            }
        }
        
        // 尝试圆括号（但要排除版本信息等）
        var parenMatch = ParenthesesRegex().Match(name);
        if (parenMatch.Success)
        {
            var content = parenMatch.Groups[1].Value;
            // 排除版本信息和其他特殊标记
            if (!VersionRegex().IsMatch(content) && 
                !content.StartsWith("DL", StringComparison.OrdinalIgnoreCase) &&
                content.Length > 2)
            {
                return content;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 清理标题，移除已提取的元信息
    /// </summary>
    private static string CleanTitle(string name, MediaKeywords keywords)
    {
        var cleaned = name;
        
        // 移除产品代码
        if (!string.IsNullOrEmpty(keywords.ProductCode))
        {
            cleaned = cleaned.Replace(keywords.ProductCode, "");
        }
        
        // 移除日期
        if (!string.IsNullOrEmpty(keywords.Date))
        {
            cleaned = cleaned.Replace(keywords.Date, "");
        }
        
        // 移除版本号
        if (!string.IsNullOrEmpty(keywords.Version))
        {
            cleaned = cleaned.Replace(keywords.Version, "");
        }
        
        // 移除社团名（包括括号）
        if (!string.IsNullOrEmpty(keywords.CircleName))
        {
            cleaned = Regex.Replace(cleaned, $@"\[{Regex.Escape(keywords.CircleName)}\]", "");
            cleaned = Regex.Replace(cleaned, $@"\({Regex.Escape(keywords.CircleName)}\)", "");
        }
        
        // 移除多余的符号和空格（包括中文括号）
        cleaned = Regex.Replace(cleaned, @"[\[\]()_\-【】（）]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim();
        
        return cleaned;
    }
    
    /// <summary>
    /// 检测文本语言（优先级：日语 > 中文 > 英文）
    /// </summary>
    private static Language DetectLanguage(string text)
    {
        var hasKana = JapaneseRegex().IsMatch(text);  // 平假名或片假名
        var hasKanji = ChineseRegex().IsMatch(text);  // 汉字
        var hasEnglish = EnglishRegex().IsMatch(text);
        
        // 优先级1：如果有假名（平假名或片假名），判定为日语
        if (hasKana)
        {
            return Language.Japanese;
        }
        
        // 优先级2：如果有汉字（没有假名），判定为中文
        if (hasKanji)
        {
            return Language.Chinese;
        }
        
        // 优先级3：如果有英文，判定为英文
        if (hasEnglish)
        {
            return Language.English;
        }
        
        // 无法识别
        return Language.Unknown;
    }
    
    /// <summary>
    /// 根据语言进行分词
    /// </summary>
    private static List<string> SegmentByLanguage(string text, Language language)
    {
        var keywords = new List<string>();
        
        switch (language)
        {
            case Language.Chinese:
                keywords = SegmentChinese(text);
                break;
                
            case Language.Japanese:
                keywords = SegmentJapanese(text);
                break;
                
            case Language.English:
                keywords = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
                break;
                
            default:
                // 未知语言，返回原文
                keywords.Add(text);
                break;
        }
        
        // 过滤掉过短的关键词
        keywords = keywords.Where(k => k.Length >= 2).ToList();
        
        return keywords;
    }
    
    /// <summary>
    /// 中文分词
    /// </summary>
    private static List<string> SegmentChinese(string text)
    {
        var segmenter = ChineseSegmenter.Value;
        if (segmenter != null)
        {
            try
            {
                var segments = segmenter.Cut(text);
                return segments.ToList();
            }
            catch
            {
                // 分词失败，使用备用方案
            }
        }
        
        // 备用方案：使用简单的字符分割
        var keywords = new List<string>();
        // 按照中文标点符号和空格分割
        var words = Regex.Split(text, @"[\s，。！？、；：""''（）【】《》]+");
        foreach (var word in words)
        {
            if (!string.IsNullOrWhiteSpace(word) && word.Length >= 2)
            {
                keywords.Add(word);
            }
        }
        
        // 如果没有分割出任何词，返回原文
        if (keywords.Count == 0)
        {
            keywords.Add(text);
        }
        
        return keywords;
    }
    
    // NMeCab的MeCabIpaDicTagger实例（延迟初始化）
    private static readonly Lazy<MeCabIpaDicTagger?> JapaneseTagger = new(() =>
    {
        try
        {
            // 使用IpaDic字典的专用Tagger
            return MeCabIpaDicTagger.Create();
        }
        catch
        {
            // 如果初始化失败，返回null
            return null;
        }
    });
    
    /// <summary>
    /// 日文分词（使用NMeCab）
    /// </summary>
    private static List<string> SegmentJapanese(string text)
    {
        var keywords = new List<string>();
        
        var tagger = JapaneseTagger.Value;
        if (tagger != null)
        {
            try
            {
                // 使用NMeCab进行分词
                var nodes = tagger.Parse(text);
                
                foreach (var node in nodes)
                {
                    // 获取表层形式（词的原形）
                    var surface = node.Surface;
                    
                    // 获取词性信息
                    var partOfSpeech = node.PartsOfSpeech;
                    
                    // 过滤规则：
                    // 1. 排除助词、助动词、符号
                    // 2. 保留名词、动词、形容词等实词
                    // 3. 保留长度大于1的词或单个汉字
                    if (partOfSpeech != "助詞" && 
                        partOfSpeech != "助動詞" &&
                        partOfSpeech != "記号" &&
                        !string.IsNullOrWhiteSpace(surface))
                    {
                        // 进一步检查：保留长度大于1的词，或单个汉字
                        if (surface.Length > 1 || 
                            Regex.IsMatch(surface, @"[\u4E00-\u9FA5]"))
                        {
                            // 对于动词和形容词，尝试获取原形（基本形）
                            var originalForm = node.OriginalForm;
                            if (!string.IsNullOrEmpty(originalForm) && 
                                originalForm != surface &&
                                (partOfSpeech.StartsWith("動詞") || partOfSpeech.StartsWith("形容詞")))
                            {
                                keywords.Add(originalForm);
                            }
                            else
                            {
                                keywords.Add(surface);
                            }
                        }
                    }
                }
            }
            catch
            {
                // NMeCab分词失败，使用备用方案
            }
        }
        
        // 如果NMeCab分词失败或没有结果，使用备用的正则表达式方案
        if (keywords.Count == 0)
        {
            // 使用正则表达式分割日文
            var pattern = @"[\u3040-\u309F]+|[\u30A0-\u30FF]+|[\u4E00-\u9FA5]+|[a-zA-Z]+";
            var matches = Regex.Matches(text, pattern);
            
            foreach (Match match in matches)
            {
                var word = match.Value;
                // 过滤掉单个假名（通常是助词）
                if (word.Length > 1 || Regex.IsMatch(word, @"[\u4E00-\u9FA5]"))
                {
                    keywords.Add(word);
                }
            }
            
            // 如果还是没有匹配到任何内容，返回原文
            if (keywords.Count == 0)
            {
                keywords.Add(text);
            }
        }
        
        return keywords;
    }
    
    // 正则表达式定义
    [GeneratedRegex(@"(RJ|BJ|VJ|RE|RG)\d{5,8}", RegexOptions.IgnoreCase)]
    private static partial Regex ProductCodeRegex();
    
    [GeneratedRegex(@"\[(\d{8}|\d{4}-\d{2}-\d{2})\]|\((\d{8}|\d{4}-\d{2}-\d{2})\)")]
    private static partial Regex DateRegex();
    
    [GeneratedRegex(@"[vV]\d+(?:\.\d+)*")]
    private static partial Regex VersionRegex();
    
    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex SquareBracketRegex();
    
    [GeneratedRegex(@"\(([^\)]+)\)")]
    private static partial Regex ParenthesesRegex();
    
    [GeneratedRegex(@"[\u3040-\u309F\u30A0-\u30FF]+")]
    private static partial Regex JapaneseRegex();
    
    [GeneratedRegex(@"[\u4E00-\u9FA5]+")]
    private static partial Regex ChineseRegex();
    
    [GeneratedRegex(@"[a-zA-Z]+")]
    private static partial Regex EnglishRegex();
}