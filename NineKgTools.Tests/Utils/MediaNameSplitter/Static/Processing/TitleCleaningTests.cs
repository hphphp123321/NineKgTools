using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Processing;

/// <summary>
/// 标题清理测试
/// </summary>
public class TitleCleaningTests
{
    [Theory]
    [InlineData("[社团] RJ12345 作品名 v1.0", "作品名")]
    [InlineData("[20240101][社团] 作品名 (DL版)", "作品名 DL版")]
    [InlineData("作品名_完整版_汉化", "作品名 完整版 汉化")]
    [InlineData("作品---名___测试", "作品 名 测试")]
    [InlineData("[][][]作品名()()", "作品名")]
    public void CleanTitle_ShouldCleanCorrectly(string input, string expectedContains)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.CleanedTitle.Should().Contain(expectedContains);
        result.CleanedTitle.Should().NotContain("[]");
        result.CleanedTitle.Should().NotContain("()");
    }
    
    [Fact]
    public void CleanTitle_ShouldRemoveMetadata()
    {
        // Arrange
        var fileName = "[20240101][ILLUSION] RJ12345 魔法少女的冒险 v1.0 (DL版)";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CleanedTitle.Should().NotContain("20240101");
        result.CleanedTitle.Should().NotContain("ILLUSION");
        result.CleanedTitle.Should().NotContain("RJ12345");
        result.CleanedTitle.Should().NotContain("v1.0");
        result.CleanedTitle.Should().Contain("魔法少女的冒险");
        result.CleanedTitle.Should().Contain("DL版");
    }
    
    [Fact]
    public void CleanTitle_ShouldNormalizeSpaces()
    {
        // Arrange
        var fileName = "作品___名   测试     版本";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CleanedTitle.Should().NotContain("  "); // 不应有连续空格
        result.CleanedTitle.Should().NotContain("_");
        result.CleanedTitle.Trim().Should().Be(result.CleanedTitle); // 不应有前后空格
    }
    
    [Fact]
    public void CleanTitle_ShouldHandleEmptyBrackets()
    {
        // Arrange
        var fileName = "[] 作品名 ()";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CleanedTitle.Should().Be("作品名");
    }
    
    [Fact]
    public void CleanTitle_ShouldPreserveImportantContent()
    {
        // Arrange
        var fileName = "[Lilith-Raws] 無職転生 / Mushoku Tensei - 01 [Baha][WEB-DL][1080p][AVC AAC][CHT][MP4]";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CleanedTitle.Should().Contain("無職転生");
        result.CleanedTitle.Should().Contain("Mushoku Tensei");
        result.CleanedTitle.Should().Contain("01");
    }
}