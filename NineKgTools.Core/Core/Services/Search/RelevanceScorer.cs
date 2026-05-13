using System;
using NineKgTools.Utils;

namespace NineKgTools.Core.Services.Search;

/// <summary>
/// 搜索相关性评分器
/// </summary>
public static class RelevanceScorer
{
    /// <summary>
    /// 计算文本相关性分数
    /// </summary>
    public static double CalculateTextRelevance(string query, string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(text))
            return 0.0;
        
        // 基础分数计算
        double score = 0.0;
        
        var queryLower = query.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();
        
        // 1. 精确匹配：1.0
        if (textLower.Equals(queryLower))
            return 1.0 * GetFieldWeight(fieldName);
        
        // 2. 开头匹配：0.8-0.9
        if (textLower.StartsWith(queryLower))
        {
            score = 0.8 + (0.1 * (query.Length / (double)text.Length));
        }
        // 3. 包含匹配：0.5-0.7
        else if (textLower.Contains(queryLower))
        {
            // 计算位置权重，越靠前分数越高
            var position = textLower.IndexOf(queryLower);
            var positionWeight = 1.0 - (position / (double)text.Length * 0.2);
            score = 0.5 + (0.2 * (query.Length / (double)text.Length)) * positionWeight;
        }
        // 4. 模糊匹配：使用编辑距离
        else
        {
            var similarity = StringSimilarityCalculator.GetAverageSimilarity(query, text);
            score = similarity * 0.7;
        }
        
        // 5. 字段权重调整
        score *= GetFieldWeight(fieldName);
        
        return Math.Min(score, 1.0);
    }
    
    /// <summary>
    /// 只支持 Exact / StartsWith / Contains 三档的相关度评分——不做编辑距离 Fuzzy 模糊匹配。
    /// 用于标签 / 创作者等"必须真正出现关键词才能命中"的场景，避免 Fuzzy 产生
    /// 用户难理解的"为什么这个也搜出来了"结果。query 不出现在 text 中则返回 0。
    /// </summary>
    public static double CalculateContainsRelevance(string query, string text, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(text))
            return 0.0;

        var queryLower = query.ToLowerInvariant();
        var textLower = text.ToLowerInvariant();

        // 精确匹配
        if (textLower.Equals(queryLower))
            return 1.0 * GetFieldWeight(fieldName);

        double score;
        // 开头匹配
        if (textLower.StartsWith(queryLower))
        {
            score = 0.8 + (0.1 * (query.Length / (double)text.Length));
        }
        // 包含匹配
        else if (textLower.Contains(queryLower))
        {
            var position = textLower.IndexOf(queryLower);
            var positionWeight = 1.0 - (position / (double)text.Length * 0.2);
            score = 0.5 + (0.2 * (query.Length / (double)text.Length)) * positionWeight;
        }
        else
        {
            // 不出现 → 不命中（不走 Fuzzy）
            return 0.0;
        }

        score *= GetFieldWeight(fieldName);
        return Math.Min(score, 1.0);
    }

    /// <summary>
    /// 获取字段权重
    /// </summary>
    private static double GetFieldWeight(string fieldName)
    {
        return fieldName?.ToLowerInvariant() switch
        {
            "title" or "name" => 1.0,
            "aliastitle" or "aliasname" or "aliastitles" or "aliasnames" => 0.9,
            "summary" => 0.7,
            "description" => 0.6,
            "summarytranslated" or "descriptiontranslated" => 0.5,
            _ => 0.5
        };
    }
    
    /// <summary>
    /// 标准化向量搜索分数
    /// </summary>
    public static double NormalizeVectorScore(double cosineSimilarity)
    {
        // 将余弦相似度 (通常在 0.7-1.0 范围) 映射到 0-1 评分
        // 使用 sigmoid 函数进行平滑映射
        if (cosineSimilarity < 0.7)
            return 0;
        
        double normalized = (cosineSimilarity - 0.7) / 0.3;
        return Math.Max(0, Math.Min(1, normalized));
    }
    
    /// <summary>
    /// 组合文本和向量搜索分数
    /// </summary>
    public static double CombineScores(double textScore, double vectorScore, double vectorWeight = 0.6)
    {
        // 如果只有一种分数，直接返回
        if (vectorScore == 0)
            return textScore;
        if (textScore == 0)
            return vectorScore;
        
        // 加权组合文本和向量分数
        return (textScore * (1 - vectorWeight)) + (vectorScore * vectorWeight);
    }
    
    /// <summary>
    /// 生成高亮文本
    /// </summary>
    public static string HighlightText(string text, string query, string highlightTag = "<mark>")
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
            return text;
        
        var closeTag = highlightTag.Replace("<", "</");
        
        // 不区分大小写的替换
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var matchedText = text.Substring(index, query.Length);
            return text.Substring(0, index) + highlightTag + matchedText + closeTag + 
                   text.Substring(index + query.Length);
        }
        
        return text;
    }
    
    /// <summary>
    /// 获取文本片段（用于显示搜索结果）
    /// </summary>
    public static string GetTextSnippet(string text, string query, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;
        
        if (text.Length <= maxLength)
            return text;
        
        // 查找查询词位置
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            // 计算片段起始位置
            var start = Math.Max(0, index - maxLength / 2);
            var length = Math.Min(maxLength, text.Length - start);
            
            var snippet = text.Substring(start, length);
            
            // 添加省略号
            if (start > 0)
                snippet = "..." + snippet;
            if (start + length < text.Length)
                snippet = snippet + "...";
            
            return snippet;
        }
        
        // 如果没找到查询词，返回开头部分
        return text.Substring(0, Math.Min(maxLength, text.Length)) + 
               (text.Length > maxLength ? "..." : "");
    }
}