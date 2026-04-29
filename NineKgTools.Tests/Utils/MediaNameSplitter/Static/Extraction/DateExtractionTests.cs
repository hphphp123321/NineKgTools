using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Extraction;

/// <summary>
/// 日期提取测试
/// </summary>
public class DateExtractionTests
{
    [Theory]
    [InlineData("[20240101] 作品名", "[20240101]")]
    [InlineData("[2024-01-01] 作品名", "[2024-01-01]")]
    [InlineData("(20231225) 作品名", "(20231225)")]
    [InlineData("(2023-12-25) 作品名", "(2023-12-25)")]
    [InlineData("普通文件名", null)]
    [InlineData("[20240101][社团名] 作品", "[20240101]")]
    [InlineData("作品名_20240101", null)] // 没有括号的日期不提取
    public void ExtractDate_ShouldExtractCorrectly(string input, string? expected)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.Date.Should().Be(expected);
    }
    
    [Fact]
    public void Date_InSquareBrackets_ShouldNotBeExtractedAsCircleName()
    {
        // Arrange
        var fileName = "[20240101] 作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Date.Should().Be("[20240101]");
        result.CircleName.Should().BeNull(); // 日期不应被识别为社团名
    }
    
    [Fact]
    public void MultipleFormats_ShouldExtractFirst()
    {
        // Arrange
        var fileName = "[20240101] (2024-01-02) 作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Date.Should().Be("[20240101]"); // 应该提取第一个
    }
    
    [Fact]
    public void Date_ShouldBeCleanedFromTitle()
    {
        // Arrange
        var fileName = "[20240101][ILLUSION] 作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Date.Should().Be("[20240101]");
        result.CleanedTitle.Should().NotContain("20240101");
        result.CleanedTitle.Should().Contain("作品名");
    }
}