using FluentAssertions;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static.Segmentation;

/// <summary>
/// 日文分词测试
/// </summary>
public class JapaneseSegmentationTests
{
    [Fact]
    public void JapaneseText_ShouldSegmentProperly()
    {
        // Arrange
        var fileName = "魔法少女まどか☆マギカ";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
        var allKeywords = result.GetAllKeywords();
        allKeywords.Should().NotBeEmpty();
        // 应该包含 "魔法少女" 或 "まどか" 或 "マギカ"
    }
    
    [Fact]
    public void PureKatakana_ShouldBeDetectedAsJapanese()
    {
        // Arrange
        var fileName = "カスタムオーダーメイド";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
        result.GetAllKeywords().Should().NotBeEmpty();
    }
    
    [Fact]
    public void PureHiragana_ShouldBeDetectedAsJapanese()
    {
        // Arrange
        var fileName = "あまあま男の娘ボイス";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
        result.GetAllKeywords().Should().NotBeEmpty();
    }
    
    [Fact]
    public void JapaneseWithKanji_ShouldSegmentCorrectly()
    {
        // Arrange
        var fileName = "無職転生";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        // 只有汉字没有假名，应该被判定为中文
        result.DetectedLanguage.Should().Be(Language.Chinese);
    }
    
    [Fact]
    public void JapaneseWithKanaAndKanji_ShouldBeJapanese()
    {
        // Arrange
        var fileName = "淫魔の繭";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese); // 有假名"の"
        result.GetAllKeywords().Should().NotBeEmpty();
    }
    
    [Fact]
    public void JapaneseWithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var fileName = "お姉ちゃんと一緒";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void JapaneseTagger_ShouldHandleFailureGracefully()
    {
        // Arrange
        var fileName = "ハニーセレクト2";
        
        // Act
        var result = NineKgTools.Utils.MediaNameSplitter.ExtractKeywords(fileName);
        
        // Assert
        result.DetectedLanguage.Should().Be(Language.Japanese);
        result.PrimaryKeyword.Should().NotBeNullOrEmpty();
        // 即使NMeCab失败，也应该有备用方案返回关键词
    }
}