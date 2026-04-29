using FluentAssertions;
using NineKgTools.Utils;

namespace NineKgTools.Tests.TestHelpers;

/// <summary>
/// 测试辅助方法
/// </summary>
public static class TestHelper
{
    /// <summary>
    /// 断言关键词包含指定的词汇
    /// </summary>
    public static void AssertKeywordsContain(MediaKeywords keywords, params string[] expectedWords)
    {
        var allKeywords = keywords.GetAllKeywords();
        
        foreach (var word in expectedWords)
        {
            allKeywords.Should().Contain(k => k.Contains(word), 
                $"关键词列表应该包含 '{word}'");
        }
    }
    
    /// <summary>
    /// 断言关键词不包含指定的词汇
    /// </summary>
    public static void AssertKeywordsNotContain(MediaKeywords keywords, params string[] unexpectedWords)
    {
        var allKeywords = keywords.GetAllKeywords();
        
        foreach (var word in unexpectedWords)
        {
            allKeywords.Should().NotContain(k => k.Contains(word), 
                $"关键词列表不应该包含 '{word}'");
        }
    }
    
    /// <summary>
    /// 生成测试用的MediaKeywords对象
    /// </summary>
    public static MediaKeywords CreateTestKeywords(
        string primaryKeyword = "测试关键词",
        string? productCode = null,
        string? circleName = null,
        Language language = Language.Chinese)
    {
        return new MediaKeywords
        {
            PrimaryKeyword = primaryKeyword,
            ProductCode = productCode,
            CircleName = circleName,
            DetectedLanguage = language,
            CleanedTitle = primaryKeyword,
            SecondaryKeywords = new List<string> { "次要1", "次要2" }
        };
    }
    
    /// <summary>
    /// 批量测试文件名列表
    /// </summary>
    public static void TestFileNameBatch(IEnumerable<string> fileNames, 
        Action<string, MediaKeywords> assertAction)
    {
        foreach (var fileName in fileNames)
        {
            var result = MediaNameSplitter.ExtractKeywords(fileName);
            assertAction(fileName, result);
        }
    }
}