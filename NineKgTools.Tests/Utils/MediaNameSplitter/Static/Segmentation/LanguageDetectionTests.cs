using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Segmentation;

/// <summary>
/// 语言检测测试
/// </summary>
public class LanguageDetectionTests
{
    [Theory]
    [InlineData("中文标题", Language.Chinese)]
    [InlineData("魔法少女的冒险", Language.Chinese)]
    [InlineData("日本語タイトル", Language.Japanese)]
    [InlineData("まどか☆マギカ", Language.Japanese)]
    [InlineData("English Title", Language.English)]
    [InlineData("The Witcher", Language.English)]
    [InlineData("混合Mixed语言", Language.Chinese)] // 没有假名，有汉字，判定为中文
    [InlineData("魔法少女まどか", Language.Japanese)] // 有假名，优先判定为日语
    [InlineData("123456", Language.Unknown)]
    [InlineData("", Language.Unknown)]
    [InlineData("中文和English", Language.Chinese)] // 没有假名，有汉字，判定为中文
    [InlineData("カタカナonly", Language.Japanese)] // 片假名
    [InlineData("ひらがなonly", Language.Japanese)] // 平假名
    public void DetectLanguage_ShouldDetectCorrectly(string input, Language expected)
    {
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(input);
        
        // Assert
        result.DetectedLanguage.Should().Be(expected);
    }
    
    [Fact]
    public void JapaneseKana_ShouldHaveHighestPriority()
    {
        // Arrange - 包含日语假名、汉字和英文
        var fileName = "English 中文 まどか Title";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
    }
    
    [Fact]
    public void ChineseWithoutKana_ShouldBeDetectedAsChinese()
    {
        // Arrange - 包含汉字和英文，但没有假名
        var fileName = "魔法少女 DL版";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Chinese);
    }
    
    [Fact]
    public void PureEnglish_ShouldBeDetectedAsEnglish()
    {
        // Arrange
        var fileName = "The Witcher Wild Hunt GOTY Edition";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.English);
    }
}