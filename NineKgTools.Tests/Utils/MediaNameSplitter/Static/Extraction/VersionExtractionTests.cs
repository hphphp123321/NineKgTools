using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Extraction;

/// <summary>
/// 版本号提取测试
/// </summary>
public class VersionExtractionTests
{
    [Theory]
    [InlineData("游戏名_v1.0", "v1.0")]
    [InlineData("游戏名 V2.1.3", "V2.1.3")]
    [InlineData("游戏名_v10", "v10")]
    [InlineData("游戏名 Version 1.0", null)] // 完整的Version不提取
    [InlineData("游戏名", null)]
    [InlineData("[ILLUSION] v1.2 game", "v1.2")]
    [InlineData("作品_V0.9.5_beta", "V0.9.5")]
    [InlineData("v1.0.0.1234 详细版本", "v1.0.0.1234")]
    public void ExtractVersion_ShouldExtractCorrectly(string input, string? expected)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.Version.Should().Be(expected);
    }
    
    [Fact]
    public void Version_ShouldNotBeMistakenAsFileExtension()
    {
        // Arrange
        var fileNames = new[] { "游戏名 v1.0", "作品_V2.1.3" };
        
        foreach (var fileName in fileNames)
        {
            // Act
            var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
            
            // Assert
            result.Version.Should().NotBeNull();
            result.Version.Should().Contain(".");
        }
    }
    
    [Fact]
    public void Version_ShouldBeCleanedFromTitle()
    {
        // Arrange
        var fileName = "游戏名 v1.0 完整版";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Version.Should().Be("v1.0");
        result.CleanedTitle.Should().NotContain("v1.0");
        result.CleanedTitle.Should().Contain("游戏名");
        result.CleanedTitle.Should().Contain("完整版");
    }
}