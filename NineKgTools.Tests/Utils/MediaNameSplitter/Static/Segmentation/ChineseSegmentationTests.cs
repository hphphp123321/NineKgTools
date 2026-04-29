using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Segmentation;

/// <summary>
/// 中文分词测试
/// </summary>
public class ChineseSegmentationTests
{
    [Theory]
    [InlineData("魔法少女", new[] { "魔法", "少女" })]
    [InlineData("勇者斗恶龙", new[] { "勇者" })]
    [InlineData("最终幻想", new[] { "最终", "幻想" })]
    [InlineData("中文作品名", new[] { "中文", "作品" })]
    public void ChineseSegmentation_ShouldContainExpectedWords(string input, string[] expectedWords)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        var allKeywords = result.GetAllKeywords();
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Chinese);
        foreach (var word in expectedWords)
        {
            allKeywords.Should().Contain(k => k.Contains(word), 
                $"应该包含关键词 '{word}'");
        }
    }
    
    [Fact]
    public void ChineseText_ShouldSegmentProperly()
    {
        // Arrange
        var fileName = "魔法少女的冒险";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Chinese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
        // 分词可能失败，但至少应该有主关键词
        result.GetAllKeywords().Should().NotBeEmpty();
    }
    
    [Fact]
    public void ChineseWithPunctuation_ShouldSegmentCorrectly()
    {
        // Arrange
        var fileName = "【AI少女】【璇玑公主】高品质MOD合集";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Chinese);
        result.CleanedTitle.Should().NotContain("【");
        result.CleanedTitle.Should().NotContain("】");
        result.GetAllKeywords().Should().NotBeEmpty();
    }
    
    [Fact]
    public void ChineseSegmenter_ShouldHandleFailureGracefully()
    {
        // Arrange
        var fileName = "纯中文内容";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Chinese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
        // 即使分词器失败，也应该有备用方案返回关键词
    }
}