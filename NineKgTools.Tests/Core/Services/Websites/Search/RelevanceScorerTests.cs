using FluentAssertions;
using NineKgTools.Core.Services.Websites;
using NineKgTools.Core.Services.Websites.Search;
using NineKgTools.Utils;
using Xunit;

namespace NineKgTools.Tests.Core.Services.Websites.Search;

/// <summary>
/// 相关性评分器测试
/// </summary>
public class RelevanceScorerTests
{
    private readonly RelevanceScorer _scorer = new();
    
    [Fact]
    public void CalculateRelevance_WithExactProductCodeMatch_ShouldReturnHighScore()
    {
        // Arrange
        var searchResult = new MediaSearchResult
        {
            Title = "[ILLUSION] RJ12345 魔法少女的冒险 v1.0",
            SearchKey = "RJ12345",
            Id = "RJ12345"
        };
        
        var keywords = new MediaKeywords
        {
            ProductCode = "RJ12345",
            PrimaryKeyword = "魔法少女",
            CircleName = "ILLUSION"
        };
        
        // Act
        var score = _scorer.CalculateRelevance(searchResult, keywords);
        
        // Assert
        score.Should().BeGreaterThan(0.8);
    }
    
    [Fact]
    public void CalculateRelevance_WithNoMatches_ShouldReturnLowScore()
    {
        // Arrange
        var searchResult = new MediaSearchResult
        {
            Title = "完全不相关的作品名称",
            SearchKey = "关键词",
            Id = "123"
        };
        
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女",
            SecondaryKeywords = new List<string> { "冒险", "RPG" }
        };
        
        // Act
        var score = _scorer.CalculateRelevance(searchResult, keywords);
        
        // Assert
        score.Should().BeLessThan(0.3);
    }
    
    [Fact]
    public void CalculateRelevance_WithHighKeywordCoverage_ShouldReturnHighScore()
    {
        // Arrange
        var searchResult = new MediaSearchResult
        {
            Title = "魔法少女的冒险RPG游戏",
            SearchKey = "魔法少女",
            Id = "123"
        };
        
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女",
            SecondaryKeywords = new List<string> { "冒险", "RPG" }
        };
        
        // Act
        var score = _scorer.CalculateRelevance(searchResult, keywords);
        
        // Assert
        score.Should().BeGreaterThan(0.5);
    }
    
    [Fact]
    public void CalculateRelevance_WithCircleNameMatch_ShouldBoostScore()
    {
        // Arrange
        var searchResult1 = new MediaSearchResult
        {
            Title = "[ILLUSION] 某个作品",
            SearchKey = "作品",
            Id = "123"
        };
        
        var searchResult2 = new MediaSearchResult
        {
            Title = "某个作品",
            SearchKey = "作品",
            Id = "456"
        };
        
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "作品",
            CircleName = "ILLUSION"
        };
        
        // Act
        var score1 = _scorer.CalculateRelevance(searchResult1, keywords);
        var score2 = _scorer.CalculateRelevance(searchResult2, keywords);
        
        // Assert
        score1.Should().BeGreaterThan(score2);
    }
    
    [Fact]
    public void CalculateRelevance_WithKeywordAtBeginning_ShouldScoreHigherThanAtEnd()
    {
        // Arrange
        var searchResult1 = new MediaSearchResult
        {
            Title = "魔法少女的冒险故事",
            SearchKey = "魔法少女",
            Id = "123"
        };
        
        var searchResult2 = new MediaSearchResult
        {
            Title = "某个故事中的魔法少女",
            SearchKey = "魔法少女",
            Id = "456"
        };
        
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女"
        };
        
        // Act
        var score1 = _scorer.CalculateRelevance(searchResult1, keywords);
        var score2 = _scorer.CalculateRelevance(searchResult2, keywords);
        
        // Assert
        score1.Should().BeGreaterThan(score2);
    }
    
    [Fact]
    public void CalculateRelevance_WithVeryLongTitle_ShouldApplyLengthPenalty()
    {
        // Arrange
        var searchResult1 = new MediaSearchResult
        {
            Title = "魔法少女",
            SearchKey = "魔法少女",
            Id = "123"
        };
        
        var searchResult2 = new MediaSearchResult
        {
            Title = "魔法少女" + string.Join("", Enumerable.Repeat("额外的内容", 20)),
            SearchKey = "魔法少女",
            Id = "456"
        };
        
        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "魔法少女",
            CleanedTitle = "魔法少女"
        };
        
        // Act
        var score1 = _scorer.CalculateRelevance(searchResult1, keywords);
        var score2 = _scorer.CalculateRelevance(searchResult2, keywords);
        
        // Assert
        score1.Should().BeGreaterThan(score2);
    }
    
    [Fact]
    public void CalculateRelevance_WithHtmlEntityAndCensoredChars_ShouldMatch()
    {
        // 回归：本地标题与搜索结果本质相同，但存在 HTML 实体 (&quot;)、全半角标点差异、
        // 审查占位符 (催眠→催○) 时，不应被长度惩罚和正则破坏到跌破 min_similarity 阈值。
        var searchResult = new MediaSearchResult
        {
            Title = "即おち!〜&quot;city girls&quot;〜脈なし女子を即堕ち催○→新妻なっちゃん妊活中♪精子競争まけませんっ♪防衛失敗!敗北妊娠!完堕ち人妻NTR托卵♪",
            SearchKey = "city girls",
            Id = "RJ000000"
        };

        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "city",
            SecondaryKeywords = new List<string>
            {
                "girls", "脈なし", "女子", "即堕ち", "催眠", "新妻", "なっちゃん",
                "妊活", "精子", "競争", "防衛", "失敗", "敗北", "妊娠", "人妻", "托卵"
            },
            CleanedTitle = "即おち city girls 脈なし女子を即堕ち催眠 新妻なっちゃん妊活中"
        };

        var score = _scorer.CalculateRelevance(searchResult, keywords);

        score.Should().BeGreaterThan(0.65);
    }

    [Fact]
    public void CalculateRelevance_HtmlEntitiesShouldNotLeakAsFakeKeywords()
    {
        // 纯正原标题不应因 &quot; 在搜索结果里而被匹配降分
        var searchResult = new MediaSearchResult
        {
            Title = "这是一个&quot;测试&quot;作品",
            SearchKey = "测试",
            Id = "T1"
        };

        var keywords = new MediaKeywords
        {
            PrimaryKeyword = "测试",
            SecondaryKeywords = new List<string> { "作品" },
            CleanedTitle = "这是一个测试作品"
        };

        var score = _scorer.CalculateRelevance(searchResult, keywords);

        score.Should().BeGreaterThan(0.6);
    }

    [Fact]
    public void CalculateRelevance_WithSearchQuery_ShouldApplyQueryTypeBonus()
    {
        // Arrange
        var searchResult = new MediaSearchResult
        {
            Title = "RJ12345 作品名",
            SearchKey = "RJ12345",
            Id = "RJ12345"
        };
        
        var keywords = new MediaKeywords
        {
            ProductCode = "RJ12345",
            PrimaryKeyword = "作品名"
        };
        
        var productCodeQuery = new SearchQuery
        {
            Query = "RJ12345",
            Type = SearchQueryType.ProductCode,
            Priority = 100
        };
        
        // Act
        var score = _scorer.CalculateRelevance(searchResult, keywords, productCodeQuery);
        
        // Assert
        score.Should().BeGreaterThan(0.8);
    }
}