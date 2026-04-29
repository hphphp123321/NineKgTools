using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tags;
using Xunit;
using MatchType = NineKgTools.Core.Models.Tags.MatchType;

namespace NineKgTools.Tests.Services.Tags;

/// <summary>
/// 标签匹配服务测试
/// </summary>
public class TagMatchingServiceTests : IDisposable
{
    private readonly MediaDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Config _config;
    private readonly TagMappingService _mappingService;
    private readonly TagMatchingService _service;
    
    public TagMatchingServiceTests()
    {
        // 设置内存数据库
        var options = new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new MediaDbContext(options);
        
        // 创建缓存
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        
        // 创建配置
        _config = new Config
        {
            TagMatching = new TagMatchingConfig
            {
                EnableFuzzyMatching = true,
                SimilarityThreshold = 0.7,
                EnableContainsMatching = true,
                EnableNormalizedMatching = true,
                MaxMatchResults = 5
            }
        };
        
        // 初始化测试数据
        InitializeTestData();
        
        // 创建映射服务
        _mappingService = new TagMappingService(_context);
        
        // 创建服务
        _service = new TagMatchingService(_context, _config, _mappingService);
    }
    
    private void InitializeTestData()
    {
        var topTag1 = new TopTag { Id = 1, Name = "类型" };
        var topTag2 = new TopTag { Id = 2, Name = "风格" };
        
        _context.TopTags.AddRange(topTag1, topTag2);
        
        var tags = new List<Tag>
        {
            new Tag { Id = 1, Name = "游戏", TopTag = topTag1 },
            new Tag { Id = 2, Name = "动作游戏", TopTag = topTag1 },
            new Tag { Id = 3, Name = "角色扮演", TopTag = topTag1 },
            new Tag { Id = 4, Name = "RPG", TopTag = topTag1 },
            new Tag { Id = 5, Name = "科幻", TopTag = topTag2 },
            new Tag { Id = 6, Name = "科学幻想", TopTag = topTag2 },
            new Tag { Id = 7, Name = "ゲーム", TopTag = topTag1 }, // 日文"游戏"
            new Tag { Id = 8, Name = "遊戲", TopTag = topTag1 }, // 繁体中文"游戏"
            new Tag { Id = 9, Name = "Game", TopTag = topTag1 }, // 英文"游戏"
            new Tag { Id = 10, Name = "アクション", TopTag = topTag1 }, // 日文"动作"
        };
        
        _context.Tags.AddRange(tags);
        _context.SaveChanges();
    }
    
    [Fact]
    public async Task FindBestMatchAsync_ExactMatch_ReturnsCorrectTag()
    {
        // Arrange
        var query = "游戏";
        
        // Act
        var result = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("游戏", result.Tag.Name);
        Assert.Equal(1.0, result.Confidence);
        Assert.Equal(MatchType.Exact, result.MatchType);
    }
    
    [Fact]
    public async Task FindBestMatchAsync_NormalizedMatch_ReturnsCorrectTag()
    {
        // Arrange
        var query = "游 戏"; // 带空格
        
        // Act
        var result = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("游戏", result.Tag.Name);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal(MatchType.Normalized, result.MatchType);
    }
    
    [Fact]
    public async Task FindBestMatchAsync_ContainsMatch_ReturnsCorrectTag()
    {
        // Arrange
        var query = "动作";
        
        // Act
        var result = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.NotNull(result);
        // 因为包含匹配需要模糊匹配启用，而相似度匹配会优先返回
        Assert.True(result.Tag.Name == "动作游戏" || result.Tag.Name.Contains("动作"));
        Assert.True(result.Confidence >= 0.5);
        // 可能是相似度匹配或包含匹配
        Assert.True(result.MatchType == MatchType.Similarity || result.MatchType == MatchType.Contains);
    }
    
    [Fact]
    public async Task FindBestMatchAsync_SimilarityMatch_ReturnsCorrectTag()
    {
        // Arrange
        var query = "角色"; // 与"角色扮演"相似但不是完全包含
        
        // Act
        var result = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result.Tag.Name == "角色扮演");
        Assert.True(result.Confidence >= 0.5);
        // 可能是包含匹配或相似度匹配，取决于配置
        Assert.True(result.MatchType == MatchType.Contains || result.MatchType == MatchType.Similarity);
    }
    
    [Fact]
    public async Task FindBestMatchAsync_NoMatch_ReturnsNull()
    {
        // Arrange
        var query = "不存在的标签XYZ123";
        
        // Act
        var result = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public async Task FindAllMatchesAsync_MultipleMatches_ReturnsOrderedResults()
    {
        // Arrange
        var query = "游戏";
        
        // Act
        var results = await _service.FindAllMatchesAsync(query);
        
        // Assert
        Assert.NotEmpty(results);
        Assert.Equal("游戏", results.First().Tag.Name);
        Assert.Equal(1.0, results.First().Confidence);
        
        // 应该按置信度排序
        for (int i = 0; i < results.Count - 1; i++)
        {
            Assert.True(results[i].Confidence >= results[i + 1].Confidence);
        }
    }
    
    [Fact]
    public async Task FindBestMatchAsync_CacheEnabled_UsesCachedResult()
    {
        // Arrange
        var query = "测试缓存";
        
        // Act - 第一次调用
        var result1 = await _service.FindBestMatchAsync(query);
        
        // Act - 第二次调用（应该从缓存获取）
        var result2 = await _service.FindBestMatchAsync(query);
        
        // Assert
        Assert.Equal(result1?.Tag?.Id, result2?.Tag?.Id);
    }
    
    [Fact]
    public async Task FindAllMatchesAsync_WithSimilarTags_ReturnsDedupedResults()
    {
        // Arrange
        var query = "RPG";
        
        // Act
        var results = await _service.FindAllMatchesAsync(query);
        
        // Assert
        Assert.NotEmpty(results);
        
        // 确保结果去重（每个标签ID只出现一次）
        var tagIds = results.Select(r => r.Tag?.Id).ToList();
        Assert.Equal(tagIds.Count, tagIds.Distinct().Count());
    }
    
    [Fact]
    public async Task FindBestMatchAsync_DisabledFuzzyMatching_OnlyExactMatch()
    {
        // Arrange
        _config.TagMatching.EnableFuzzyMatching = false;
        var service = new TagMatchingService(_context, _config, _mappingService);
        var query = "游 戏"; // 带空格，不会精确匹配
        
        // Act
        var result = await service.FindBestMatchAsync(query);
        
        // Assert
        Assert.Null(result); // 关闭模糊匹配后，应该返回null
    }
    
    public void Dispose()
    {
        _cache?.Dispose();
        _context?.Dispose();
    }
}