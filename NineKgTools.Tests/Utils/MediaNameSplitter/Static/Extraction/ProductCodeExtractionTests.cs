using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Extraction;

/// <summary>
/// 产品代码提取测试
/// </summary>
public class ProductCodeExtractionTests
{
    [Theory]
    [InlineData("RJ12345_作品名", "RJ12345")]
    [InlineData("[社团] BJ566243 漫画名", "BJ566243")]
    [InlineData("VJ014316-美少女游戏", "VJ014316")]
    [InlineData("RE123456_音声作品", "RE123456")]
    [InlineData("RG01234567_游戏", "RG01234567")]
    [InlineData("普通文件名没有代码", null)]
    [InlineData("[20240101] RJ999999 作品", "RJ999999")]
    [InlineData("rj88888_小写也应该识别", "rj88888")]
    [InlineData("RJ111111 [Another Code BJ222222] 作品名", "RJ111111")] // 多个代码取第一个
    public void ExtractProductCode_ShouldExtractCorrectly(string input, string? expected)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.ProductCode.Should().Be(expected);
    }
    
    [Fact]
    public void ProductCode_ShouldBeBestSearchKeyword_WhenPresent()
    {
        // Arrange
        var fileName = "RJ12345_作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.GetBestSearchKeyword().Should().Be("RJ12345");
        result.ProductCode.Should().Be("RJ12345");
    }
    
    [Fact]
    public void NoProductCode_ShouldUsePrimaryKeyword()
    {
        // Arrange
        var fileName = "普通作品名";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.ProductCode.Should().BeNull();
        result.GetBestSearchKeyword().Should().NotBeNullOrEmpty();
        result.GetBestSearchKeyword().Should().NotBe("RJ");
    }
}