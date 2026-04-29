using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Processing;

/// <summary>
/// 关键词提取测试
/// </summary>
public class KeywordExtractionTests
{
    [Theory]
    [InlineData("RJ12345_作品名", "RJ12345")] // 有产品代码时优先返回产品代码
    [InlineData("[社团] 魔法少女", "魔法")] // 中文分词后的第一个词
    [InlineData("The Witcher 3", "The")] // 英文分词后的第一个词
    public void GetBestSearchKeyword_ShouldReturnCorrectKeyword(string input, string expectedContains)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        var bestKeyword = result.GetBestSearchKeyword();
        
        // Assert
        bestKeyword.Should().Contain(expectedContains);
    }
    
    [Fact]
    public void PrimaryKeyword_ShouldNotBeEmpty()
    {
        // Arrange
        var fileNames = new[]
        {
            "普通文件名",
            "[社团] 作品名",
            "RJ12345 作品",
            ""
        };
        
        foreach (var fileName in fileNames)
        {
            // Act
            var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
            
            // Assert
            if (!string.IsNullOrEmpty(fileName))
            {
                result.PrimaryKeyword.Should().NotBeNullOrEmpty($"文件名 '{fileName}' 应该有主关键词");
            }
        }
    }
    
    [Fact]
    public void VeryLongFileName_ShouldBeTruncated()
    {
        // Arrange
        var longName = string.Join("", Enumerable.Repeat("很长的文件名", 20));
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(longName);
        
        // Assert
        result.PrimaryKeyword.Length.Should().BeLessOrEqualTo(50);
    }
    
    [Fact]
    public void GetAllKeywords_ShouldIncludeAllRelevantKeywords()
    {
        // Arrange
        var fileName = "[KISS] 魔法少女 v1.0";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        var allKeywords = result.GetAllKeywords();
        
        // Assert
        allKeywords.Should().NotBeEmpty();
        allKeywords.Should().Contain("KISS"); // 社团名应该被包含
        allKeywords.Should().Contain(k => k.Contains("魔法") || k == result.PrimaryKeyword);
    }
    
    [Fact]
    public void GetAllKeywords_ShouldNotHaveDuplicates()
    {
        // Arrange
        var fileName = "[社团] 社团 作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        var allKeywords = result.GetAllKeywords();
        
        // Assert
        allKeywords.Should().OnlyHaveUniqueItems();
    }
    
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("!!!", "!!!")]
    public void EdgeCases_ShouldHandleGracefully(string input, string expectedPrimary)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.PrimaryKeyword.Should().Be(expectedPrimary);
    }
}