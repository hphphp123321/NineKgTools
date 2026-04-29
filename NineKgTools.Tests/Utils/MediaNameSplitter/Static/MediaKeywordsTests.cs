using FluentAssertions;
using NineKgTools.Utils;

namespace NineKgTools.Tests.Utils.MediaNameSplitter.Static;

/// <summary>
/// MediaKeywords 类的单元测试
/// </summary>
public class MediaKeywordsTests
{
    [Fact]
    public void GetBestSearchKeyword_WithProductCode_ShouldReturnProductCode()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            ProductCode = "RJ12345",
            PrimaryKeyword = "作品名",
            CircleName = "社团名"
        };
        
        // Act
        var result = keywords.GetBestSearchKeyword();
        
        // Assert
        result.Should().Be("RJ12345");
    }
    
    [Fact]
    public void GetBestSearchKeyword_WithoutProductCode_ShouldReturnPrimaryKeyword()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            ProductCode = null,
            PrimaryKeyword = "作品名",
            CircleName = "社团名"
        };
        
        // Act
        var result = keywords.GetBestSearchKeyword();
        
        // Assert
        result.Should().Be("作品名");
    }
    
    [Fact]
    public void GetBestSearchKeyword_EmptyProductCode_ShouldReturnPrimaryKeyword()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            ProductCode = "",
            PrimaryKeyword = "作品名",
            CircleName = "社团名"
        };
        
        // Act
        var result = keywords.GetBestSearchKeyword();
        
        // Assert
        result.Should().Be("作品名");
    }
    
    [Fact]
    public void GetAllKeywords_ShouldReturnDistinctKeywords()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "主关键词",
            SecondaryKeywords = new List<string> { "次要1", "次要2", "主关键词" }, // 包含重复
            CircleName = "社团名"
        };
        
        // Act
        var result = keywords.GetAllKeywords();
        
        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain("主关键词");
        result.Should().Contain("次要1");
        result.Should().Contain("次要2");
        result.Should().Contain("社团名");
        result.Should().OnlyHaveUniqueItems();
    }
    
    [Fact]
    public void GetAllKeywords_WithNullCircleName_ShouldNotIncludeNull()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "主关键词",
            SecondaryKeywords = new List<string> { "次要1" },
            CircleName = null
        };
        
        // Act
        var result = keywords.GetAllKeywords();
        
        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContainNulls();
    }
    
    [Fact]
    public void GetAllKeywords_WithEmptyPrimaryKeyword_ShouldNotIncludeEmpty()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "",
            SecondaryKeywords = new List<string> { "次要1" },
            CircleName = "社团名"
        };
        
        // Act
        var result = keywords.GetAllKeywords();
        
        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain("");
    }
    
    [Fact]
    public void DefaultValues_ShouldBeInitialized()
    {
        // Arrange & Act
        var keywords = new MediaKeywords();
        
        // Assert
        keywords.PrimaryKeyword.Should().Be(string.Empty);
        keywords.SecondaryKeywords.Should().NotBeNull();
        keywords.SecondaryKeywords.Should().BeEmpty();
        keywords.ProductCode.Should().BeNull();
        keywords.CircleName.Should().BeNull();
        keywords.CleanedTitle.Should().Be(string.Empty);
        keywords.DetectedLanguage.Should().Be(Language.Unknown);
        keywords.Version.Should().BeNull();
        keywords.Date.Should().BeNull();
    }
}