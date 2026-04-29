using System.Text.RegularExpressions;
using SimMetrics.Net.Metric;

namespace NineKgTools.Utils;

public static partial class StringSimilarityCalculator
{
    private static readonly OverlapCoefficient Overlap = new();
    private static readonly CosineSimilarity Cosine = new();
    private static readonly Levenstein Levenshtein = new();
    private static readonly JaroWinkler JaroWinkler = new();
    private static readonly JaccardSimilarity Jaccard = new();
    private static readonly QGramsDistance QGrams = new();

    /// <summary>
    /// 获取两个字符串的平均相似度，综合了重叠系数、余弦相似度、编辑距离、Jaro-Winkler距离、Jaccard相似度、QGrams距离
    /// </summary>
    public static double GetAverageSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        
        // 全部new一遍，不用担心线程安全问题
        var overlapCoefficientSimilarity = new OverlapCoefficient().GetSimilarity(firstWord, secondWord);
        var cosineSimilarity = new CosineSimilarity().GetSimilarity(firstWord, secondWord);
        var levenshteinSimilarity = new Levenstein().GetSimilarity(firstWord, secondWord);
        var jaroWinklerSimilarity = new JaroWinkler().GetSimilarity(firstWord, secondWord);
        var jaccardSimilarity = new JaccardSimilarity().GetSimilarity(firstWord, secondWord);
        var qGramsSimilarity = new QGramsDistance().GetSimilarity(firstWord, secondWord);
        
        return (overlapCoefficientSimilarity + cosineSimilarity + levenshteinSimilarity + jaroWinklerSimilarity +
                jaccardSimilarity + qGramsSimilarity) / 6;
    }

    /// <summary>
    /// 重叠系数: 两个字符串之间的重叠系数，即两个字符串的交集元素个数除以最小字符串的长度。适合计算两个字符串之间的相似度
    /// </summary>
    public static double GetOverlapCoefficientSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return Overlap.GetSimilarity(firstWord, secondWord);
    }

    /// <summary>
    /// 余弦相似度: 两个字符串之间的余弦相似度，即两个字符串的交集元素个数除以两个字符串的乘积。适合计算两个字符串之间的相似度
    /// </summary>
    public static double GetCosineSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return Cosine.GetSimilarity(firstWord, secondWord);
    }

    /// <summary>
    /// 编辑距离（Levenshtein距离）: 计算两个字符串之间转换所需的最少单字符编辑（插入、删除或替换）次数。应用广泛，适合计算短文本之间的相似度。
    /// </summary>
    public static double GetLevenshteinSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return Levenshtein.GetSimilarity(firstWord, secondWord);
    }

    /// <summary>
    /// Jaro-Winkler距离: 是Jaro距离的变种，对字符串前面的字符给予更多权重，认为前面字符的匹配更重要。适合处理人名、地名等短字符串的相似度计算
    /// </summary>
    public static double GetJaroWinklerSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return JaroWinkler.GetSimilarity(firstWord, secondWord);
    }

    /// <summary>
    /// Jaccard相似度: 两个集合交集元素个数除以并集元素个数。适合计算两个集合之间的相似度
    /// </summary>
    public static double GetJaccardSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return Jaccard.GetSimilarity(firstWord, secondWord);
    }

    /// <summary>
    /// QGrams距离: 两个字符串之间的QGrams距离，QGrams是字符串中的连续子序列。适合计算两个字符串之间的相似度
    /// </summary>
    public static double GetQGramsSimilarity(string firstWord, string secondWord)
    {
        PreprocessString(ref firstWord, ref secondWord);
        return QGrams.GetSimilarity(firstWord, secondWord);
    }


    # region 私有方法

    private static void PreprocessString(ref string firstWord, ref string secondWord)
    {
        firstWord = NormalizeString(firstWord);
        secondWord = NormalizeString(secondWord);
    }

    private static string NormalizeString(string input)
    {
        // 将字符串转换为小写（对英文有效）
        input = input.ToLowerInvariant();

        // 使用正则表达式移除所有非字母数字字符（适用于英文、中文、日文）
        input = CharacterRemoveRegex().Replace(input, "");

        // 移除额外的空格（如果有的话）
        input = SpaceRemoveRegex().Replace(input, " ").Trim();

        return input;
    }

    [GeneratedRegex("[^a-zA-Z0-9\u4e00-\u9fa5\u3040-\u309F\u30A0-\u30FF]")]
    private static partial Regex CharacterRemoveRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex SpaceRemoveRegex();

    # endregion
}