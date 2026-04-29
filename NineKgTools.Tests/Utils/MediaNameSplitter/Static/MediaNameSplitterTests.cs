using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static;

/// <summary>
/// MediaNameSplitter 的综合测试
/// </summary>
public class MediaNameSplitterTests
{
    [Fact]
    public void ComplexFileName_ShouldBeProcessedCorrectly()
    {
        // Arrange
        var complexFileName = "[20240101][ILLUSION] RJ12345 魔法少女的冒险 v1.0 (DL版)";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(complexFileName);
        
        // Assert
        result.ProductCode.Should().Be("RJ12345");
        result.CircleName.Should().Be("ILLUSION");
        result.Version.Should().Be("v1.0");
        result.Date.Should().Be("[20240101]");
        result.CleanedTitle.Should().NotBeNullOrEmpty();
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
        result.DetectedLanguage.Should().Be(Language.Chinese); // 主体是中文，虽然有"DL"
    }

    [Fact]
    public void JapaneseMixedFileName_ShouldBeProcessedCorrectly()
    {
        // Arrange
        var fileName = "[サークル名] 魔法少女まどか☆マギカ 第01话";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.CircleName.Should().Be("サークル名");
        result.CleanedTitle.Should().Contain("魔法少女");
        result.DetectedLanguage.Should().Be(Language.Japanese); // 有假名，优先判定为日语
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void DLsiteFormat_ShouldExtractAllComponents()
    {
        // Arrange
        var fileName = "[20240101][ILLUSION] RJ12345 ハニーセレクト2 v1.2 (DL版)";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.Date.Should().Be("[20240101]");
        result.CircleName.Should().Be("ILLUSION");
        result.ProductCode.Should().Be("RJ12345");
        result.Version.Should().Be("v1.2");
        result.CleanedTitle.Should().Contain("ハニーセレクト");
        result.DetectedLanguage.Should().Be(Language.Japanese); // 有片假名
    }
    
    [Fact]
    public void NoMetadata_ShouldStillWork()
    {
        // Arrange
        var fileName = "普通的文件名没有任何元数据";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.ProductCode.Should().BeNull();
        result.CircleName.Should().BeNull();
        result.Version.Should().BeNull();
        result.Date.Should().BeNull();
        result.CleanedTitle.Should().Be("普通的文件名没有任何元数据");
        result.DetectedLanguage.Should().Be(Language.Chinese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void BackwardCompatibility_OldInterface()
    {
        // Arrange
        var fileName = "RJ12345_作品名";
        
        // Act - 测试旧接口
        var oldResult = NineKgTools.Utils.MediaNameSplitter.SplitKeyword(fileName);
        
        // Assert
        oldResult.Should().Be("RJ12345");
    }
}