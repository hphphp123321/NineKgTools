using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Search;

/// <summary>
/// 搜索结果相关性评分器，使用多维度评分替代简单的字符串相似度
/// </summary>
public class RelevanceScorer
{
    /// <summary>
    /// 计算搜索结果的相关性评分
    /// </summary>
    /// <param name="searchResult">搜索结果</param>
    /// <param name="originalKeywords">原始关键词信息</param>
    /// <param name="searchQuery">使用的搜索查询</param>
    /// <returns>相关性评分（0-1之间，越高越相关）</returns>
    public double CalculateRelevance(MediaSearchResult searchResult, MediaKeywords originalKeywords, SearchQuery? searchQuery = null)
    {
        var scores = new List<WeightedScore>();
        var resultTitle = searchResult.Title.ToLowerInvariant();

        // 0. 标题直接相似度快速通道：
        //    绕过分词/简繁体差异导致的关键词覆盖率偏低问题。
        //    从 PrimaryKeyword + CleanedTitle 还原完整标题，与搜索结果做有序子序列匹配。
        var titleSimilarity = CalculateTitleContainment(resultTitle, originalKeywords);
        if (titleSimilarity >= 0.85)
        {
            Log.Debug("标题直接相似度快速通道: {Score} for {Title}", titleSimilarity, searchResult.Title);
            return titleSimilarity;
        }

        // 1. 产品代码精确匹配（权重最高）
        if (!string.IsNullOrEmpty(originalKeywords.ProductCode))
        {
            var productCodeScore = CalculateProductCodeMatch(resultTitle, originalKeywords.ProductCode);
            scores.Add(new WeightedScore(productCodeScore, 0.35));
            
            Log.Debug("产品代码匹配得分: {Score} for {Title}", productCodeScore, searchResult.Title);
        }
        
        // 2. 关键词覆盖率
        var coverageScore = CalculateKeywordCoverage(resultTitle, originalKeywords);
        scores.Add(new WeightedScore(coverageScore, 0.25));
        
        Log.Debug("关键词覆盖率得分: {Score} for {Title}", coverageScore, searchResult.Title);
        
        // 3. 关键词位置权重
        var positionScore = CalculateKeywordPosition(resultTitle, originalKeywords);
        scores.Add(new WeightedScore(positionScore, 0.15));
        
        Log.Debug("关键词位置得分: {Score} for {Title}", positionScore, searchResult.Title);
        
        // 4. 社团名匹配
        if (!string.IsNullOrEmpty(originalKeywords.CircleName))
        {
            var circleScore = CalculateCircleNameMatch(resultTitle, originalKeywords.CircleName);
            scores.Add(new WeightedScore(circleScore, 0.15));
            
            Log.Debug("社团名匹配得分: {Score} for {Title}", circleScore, searchResult.Title);
        }
        
        // 5. 查询类型加权
        if (searchQuery != null)
        {
            var queryTypeScore = GetQueryTypeScore(searchQuery.Type);
            scores.Add(new WeightedScore(queryTypeScore, 0.1));
            
            Log.Debug("查询类型得分: {Score} for type {Type}", queryTypeScore, searchQuery.Type);
        }
        
        // 计算加权总分
        var totalWeight = scores.Sum(s => s.Weight);
        var finalScore = scores.Sum(s => s.Score * s.Weight) / totalWeight;

        // 高覆盖率保底：当其他信号缺失时，避免高覆盖率被加权均值稀释导致误过滤
        if (coverageScore >= 0.85 && positionScore >= 0.3)
        {
            finalScore = Math.Max(finalScore, 0.65);
        }

        // 应用长度惩罚（标题过长可能相关性较低）
        finalScore *= CalculateLengthPenalty(resultTitle, originalKeywords.CleanedTitle);

        Log.Debug("最终相关性得分: {Score} for {Title}", finalScore, searchResult.Title);
        
        return Math.Min(1.0, Math.Max(0.0, finalScore));
    }
    
    /// <summary>
    /// 计算产品代码匹配得分
    /// </summary>
    private double CalculateProductCodeMatch(string title, string productCode)
    {
        var normalizedCode = productCode.ToUpperInvariant();
        var normalizedTitle = title.ToUpperInvariant();
        
        // 精确匹配
        if (normalizedTitle.Contains(normalizedCode))
        {
            return 1.0;
        }
        
        // 部分匹配（如 RJ12345 vs RJ012345）
        var codePattern = Regex.Replace(normalizedCode, @"\d+", @"\d+");
        if (Regex.IsMatch(normalizedTitle, codePattern))
        {
            return 0.8;
        }
        
        return 0.0;
    }
    
    /// <summary>
    /// 计算关键词覆盖率
    /// </summary>
    private double CalculateKeywordCoverage(string title, MediaKeywords keywords)
    {
        var allKeywords = keywords.GetAllKeywords();
        if (allKeywords.Count == 0) return 0.0;
        
        var matchedCount = 0;
        var normalizedTitle = NormalizeForComparison(title);
        
        foreach (var keyword in allKeywords)
        {
            var normalizedKeyword = NormalizeForComparison(keyword);
            if (normalizedTitle.Contains(normalizedKeyword))
            {
                matchedCount++;
            }
            else
            {
                // 尝试部分匹配（针对中文分词可能不完全一致的情况）
                if (HasPartialMatch(normalizedTitle, normalizedKeyword))
                {
                    matchedCount += 1; // 部分匹配也算一个匹配
                }
            }
        }
        
        return matchedCount / (double)allKeywords.Count;
    }
    
    /// <summary>
    /// 计算关键词位置得分
    /// </summary>
    private double CalculateKeywordPosition(string title, MediaKeywords keywords)
    {
        var normalizedTitle = NormalizeForComparison(title);
        var primaryKeyword = NormalizeForComparison(keywords.PrimaryKeyword);
        
        if (string.IsNullOrEmpty(primaryKeyword)) return 0.0;
        
        var index = normalizedTitle.IndexOf(primaryKeyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return 0.0;
        
        // 位置越靠前得分越高
        var positionRatio = 1.0 - (index / (double)normalizedTitle.Length);
        return positionRatio;
    }
    
    /// <summary>
    /// 计算社团名匹配得分
    /// </summary>
    private double CalculateCircleNameMatch(string title, string circleName)
    {
        var normalizedTitle = NormalizeForComparison(title);
        var normalizedCircle = NormalizeForComparison(circleName);
        
        // 精确匹配
        if (normalizedTitle.Contains(normalizedCircle))
        {
            return 1.0;
        }
        
        // 括号内匹配
        var bracketPattern = $@"[\[\(].*{Regex.Escape(normalizedCircle)}.*[\]\)]";
        if (Regex.IsMatch(normalizedTitle, bracketPattern, RegexOptions.IgnoreCase))
        {
            return 0.9;
        }
        
        // 部分匹配
        if (HasPartialMatch(normalizedTitle, normalizedCircle))
        {
            return 0.5;
        }
        
        return 0.0;
    }
    
    /// <summary>
    /// 获取查询类型的基础得分
    /// </summary>
    private double GetQueryTypeScore(SearchQueryType type)
    {
        return type switch
        {
            SearchQueryType.ProductCode => 1.0,
            SearchQueryType.FullTitle => 0.8,
            SearchQueryType.MultiKeyword => 0.6,
            SearchQueryType.SingleKeyword => 0.4,
            SearchQueryType.CircleName => 0.3,
            _ => 0.2
        };
    }
    
    /// <summary>
    /// 计算长度惩罚系数（内容感知）。
    /// 如果短串的字符（多重集）大部分出现在长串里，说明是同一标题的完整/片段关系，不惩罚。
    /// 只有当两个标题长度差异大且字符重叠低时才惩罚。
    /// </summary>
    private double CalculateLengthPenalty(string resultTitle, string originalTitle)
    {
        if (string.IsNullOrEmpty(originalTitle) || string.IsNullOrEmpty(resultTitle)) return 1.0;

        // 归一化后再比长度 / 字符重叠，避免 HTML 实体、全角/半角差异歪曲长度
        var normResult = NormalizeForComparison(resultTitle);
        var normOriginal = NormalizeForComparison(originalTitle);

        if (normOriginal.Length == 0 || normResult.Length == 0) return 1.0;

        var maxLen = Math.Max(normResult.Length, normOriginal.Length);
        var minLen = Math.Min(normResult.Length, normOriginal.Length);
        var lengthRatio = maxLen / (double)minLen;

        // 差距不大，不惩罚
        if (lengthRatio <= 2.0) return 1.0;

        // 用字符多重集交集判断"短串是否基本被长串包住"
        var shorter = normResult.Length <= normOriginal.Length ? normResult : normOriginal;
        var longer = normResult.Length > normOriginal.Length ? normResult : normOriginal;

        var longerCounts = new Dictionary<char, int>();
        foreach (var c in longer)
        {
            if (char.IsWhiteSpace(c)) continue;
            longerCounts[c] = longerCounts.GetValueOrDefault(c) + 1;
        }

        var common = 0;
        var shorterMeaningful = 0;
        foreach (var c in shorter)
        {
            if (char.IsWhiteSpace(c)) continue;
            shorterMeaningful++;
            if (longerCounts.TryGetValue(c, out var cnt) && cnt > 0)
            {
                common++;
                longerCounts[c] = cnt - 1;
            }
        }

        if (shorterMeaningful == 0) return 1.0;

        var containmentRatio = common / (double)shorterMeaningful;

        // 短串的字符绝大部分都在长串里 → 同一标题的片段关系，不惩罚
        // 但要求短串至少有 6 个有意义字符，避免"2-4 个字符的关键词恰好在任何长标题里都能找到"导致的假阳性
        if (containmentRatio >= 0.85 && shorterMeaningful >= 6) return 1.0;

        // 否则按长度比做柔和惩罚（系数 0.5，比原来更轻）
        return 1.0 / (1.0 + 0.5 * Math.Log(lengthRatio));
    }
    
    // 审查占位符（马赛克符号），在归一化时直接剥离，让 "催○" → "催"
    private static readonly char[] CensorMarkers = { '○', '◯', '〇', '●', '＊', '×', '✕', '☓' };

    /// <summary>
    /// 标准化字符串用于比较
    /// </summary>
    private string NormalizeForComparison(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // 1. 解码 HTML 实体（&quot; &amp; &lt; ...），避免引入假词 "quot" 等
        input = WebUtility.HtmlDecode(input);

        // 2. NFKC 兼容性归一化：全角 → 半角（！→!、～→~、全角字母数字 → 半角）
        //    不影响平假名/片假名
        input = input.Normalize(NormalizationForm.FormKC);

        // 3. 剥离审查占位符（不用空格替换，让相邻字符贴在一起）
        if (input.IndexOfAny(CensorMarkers) >= 0)
        {
            var sb = new StringBuilder(input.Length);
            foreach (var c in input)
            {
                if (Array.IndexOf(CensorMarkers, c) < 0) sb.Append(c);
            }
            input = sb.ToString();
        }

        // 4. 转换为小写
        input = input.ToLowerInvariant();

        // 5. 移除特殊字符但保留中文、日文
        input = Regex.Replace(input, @"[^\w\u4e00-\u9fff\u3040-\u309f\u30a0-\u30ff\s]", " ");

        // 6. 合并多个空格
        input = Regex.Replace(input, @"\s+", " ");

        return input.Trim();
    }
    
    /// <summary>
    /// 检查是否有部分匹配
    /// </summary>
    private bool HasPartialMatch(string text, string keyword)
    {
        if (keyword.Length <= 2) return false;

        // 对于较长的关键词，检查是否有50%以上的连续字符匹配
        // （降低至 50% 是为了让审查占位符导致的单字符缺失不破坏部分匹配，例如 "催眠" → "催"）
        var minMatchLength = Math.Max(2, (int)(keyword.Length * 0.5));
        
        for (int i = 0; i <= keyword.Length - minMatchLength; i++)
        {
            var substring = keyword.Substring(i, minMatchLength);
            if (text.Contains(substring))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// 计算标题的有序字符子序列包含率。
    /// 从关键词还原完整标题，与搜索结果标题做贪心 LCS 匹配，
    /// 返回匹配字符数 / 还原标题字符数。
    /// 天然容忍少量 CJK 简繁体差异（如 记↔記 只损失 1 个字符）。
    /// </summary>
    private double CalculateTitleContainment(string resultTitle, MediaKeywords keywords)
    {
        var fullTitle = ReconstructFullTitle(keywords);
        if (string.IsNullOrEmpty(fullTitle)) return 0.0;

        var normFull = NormalizeForComparison(fullTitle).Replace(" ", "");
        var normResult = NormalizeForComparison(resultTitle).Replace(" ", "");

        if (normFull.Length == 0 || normResult.Length == 0) return 0.0;

        // 还原标题太短时不走快速通道，避免短关键词误匹配所有包含它的标题
        if (normFull.Length < 6) return 0.0;

        // 精确子串包含
        if (normResult.Contains(normFull)) return 1.0;

        // 贪心有序子序列匹配：遍历较短串的每个字符，在较长串中按顺序查找
        var shorter = normFull.Length <= normResult.Length ? normFull : normResult;
        var longer = normFull.Length > normResult.Length ? normFull : normResult;

        var matched = 0;
        var longerIdx = 0;
        foreach (var ch in shorter)
        {
            while (longerIdx < longer.Length)
            {
                if (longer[longerIdx] == ch)
                {
                    matched++;
                    longerIdx++;
                    break;
                }
                longerIdx++;
            }
            if (longerIdx >= longer.Length) break;
        }

        return shorter.Length > 0 ? matched / (double)shorter.Length : 0.0;
    }

    /// <summary>
    /// 从关键词信息还原完整标题（PrimaryKeyword + CleanedTitle 去重拼接）
    /// </summary>
    private string ReconstructFullTitle(MediaKeywords keywords)
    {
        var primary = keywords.PrimaryKeyword ?? "";
        var cleaned = keywords.CleanedTitle ?? "";

        if (string.IsNullOrEmpty(cleaned)) return primary;
        if (string.IsNullOrEmpty(primary)) return cleaned;

        var normCleaned = NormalizeForComparison(cleaned);
        var normPrimary = NormalizeForComparison(primary);
        if (normCleaned.Contains(normPrimary)) return cleaned;

        return primary + cleaned;
    }

    /// <summary>
    /// 加权得分结构
    /// </summary>
    private struct WeightedScore
    {
        public double Score { get; }
        public double Weight { get; }
        
        public WeightedScore(double score, double weight)
        {
            Score = score;
            Weight = weight;
        }
    }
}