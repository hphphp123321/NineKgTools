using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Extraction;

/// <summary>
/// 社团名提取测试
/// </summary>
public class CircleNameExtractionTests
{
    [Theory]
    [InlineData("[ILLUSION] 游戏名", "ILLUSION")]
    [InlineData("[社团名] 作品名", "社团名")]
    [InlineData("[20240101] 作品名", null)] // 日期不应被识别为社团
    [InlineData("(DL版) 作品名", null)] // DL版不应被识别为社团
    [InlineData("[社团A][社团B] 作品", "社团A")] // 多个括号取第一个
    [InlineData("普通文件名", null)]
    [InlineData("(社团名) 作品", "社团名")] // 圆括号中的社团名
    [InlineData("(v1.0) 作品", null)] // 版本号不应被识别为社团
    [InlineData("[WEB-DL] 作品", null)] // 特殊标记不应被识别为社团
    [InlineData("[1080p] 作品", null)] // 视频规格不应被识别为社团
    public void ExtractCircleName_ShouldExtractCorrectly(string input, string? expected)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.CircleName.Should().Be(expected);
    }
    
    [Fact]
    public void CircleName_InSquareBrackets_ShouldBeExtracted()
    {
        // Arrange
        var fileName = "[梦之音汉化组] RJ345678 魔法少女";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("梦之音汉化组");
    }
    
    [Fact]
    public void ComicMarketFormat_ShouldExtractCircleWithAuthor()
    {
        // Arrange
        var fileName = "(C97) [サークル名 (作者名)] 作品タイトル";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("サークル名 (作者名)");
    }
    
    [Fact]
    public void CircleName_ShouldBeIncludedInAllKeywords()
    {
        // Arrange
        var fileName = "[KISS] カスタムオーダーメイド3D2";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("KISS");
        result.GetAllKeywords().Should().Contain("KISS");
    }
}