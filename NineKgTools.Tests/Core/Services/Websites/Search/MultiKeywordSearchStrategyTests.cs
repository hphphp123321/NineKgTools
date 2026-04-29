using FluentAssertions;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Core.Services.Websites.Search;

/// <summary>
/// 多关键词搜索策略测试
/// </summary>
public class MultiKeywordSearchStrategyTests
{
    private readonly MultiKeywordSearchStrategy _strategy = new();
    
    [Fact]
    public void GenerateSearchQueries_WithProductCode_ShouldPrioritizeProductCode()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            ProductCode = "RJ12345",
            PrimaryKeyword = "魔法少女",
            CircleName = "ILLUSION"
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords);
        
        // Assert
        queries.Should().NotBeEmpty();
        var firstQuery = queries.First();
        firstQuery.Query.Should().Be("RJ12345");
        firstQuery.Type.Should().Be(SearchQueryType.ProductCode);
        firstQuery.Priority.Should().Be(100);
    }
    
    [Fact]
    public void GenerateSearchQueries_WithCircleAndPrimaryKeyword_ShouldGenerateCombination()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女",
            CircleName = "ILLUSION",
            SecondaryKeywords = new List<string> { "冒险", "RPG" }
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords);
        
        // Assert
        queries.Should().ContainSingle(q => 
            q.Query == "ILLUSION 魔法少女" && 
            q.Type == SearchQueryType.MultiKeyword);
    }
    
    [Fact]
    public void GenerateSearchQueries_WithMultipleKeywords_ShouldGenerateMultipleCombinations()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女",
            SecondaryKeywords = new List<string> { "冒险", "RPG", "3D" },
            CleanedTitle = "魔法少女的冒险"
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords, " ");
        
        // Assert
        queries.Should().HaveCountGreaterThan(3);
        // 应该包含完整标题搜索
        queries.Should().ContainSingle(q => 
            q.Query == "魔法少女的冒险" && 
            q.Type == SearchQueryType.FullTitle);
        // 应该包含主+次关键词组合
        queries.Should().ContainSingle(q => 
            q.Query == "魔法少女 冒险" && 
            q.Type == SearchQueryType.MultiKeyword);
        // 应该包含单关键词搜索
        queries.Should().ContainSingle(q => 
            q.Query == "魔法少女" && 
            q.Type == SearchQueryType.SingleKeyword);
    }
    
    [Fact]
    public void GenerateSearchQueries_ShouldOrderByPriority()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            ProductCode = "RJ12345",
            PrimaryKeyword = "作品名",
            CircleName = "社团名",
            SecondaryKeywords = new List<string> { "标签1" },
            CleanedTitle = "完整作品名"
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords);
        
        // Assert
        queries.Should().BeInDescendingOrder(q => q.Priority);
        queries.First().Type.Should().Be(SearchQueryType.ProductCode);
    }
    
    [Fact]
    public void GenerateSearchQueries_ShouldRemoveDuplicates()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "关键词",
            SecondaryKeywords = new List<string> { "关键词" }, // 重复的关键词
            CircleName = "关键词" // 也是重复的
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords);
        
        // Assert
        var uniqueQueries = queries.Select(q => q.Query).Distinct().ToList();
        uniqueQueries.Count.Should().Be(queries.Count);
    }
    
    [Fact]
    public void GenerateSearchQueries_WithCustomSeparator_ShouldUseSeparator()
    {
        // Arrange
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法",
            SecondaryKeywords = new List<string> { "少女" }
        };
        
        // Act
        var queries = _strategy.GenerateSearchQueries(keywords, "|");
        
        // Assert
        queries.Should().ContainSingle(q => 
            q.Query == "魔法|少女" && 
            q.Type == SearchQueryType.MultiKeyword);
    }
}