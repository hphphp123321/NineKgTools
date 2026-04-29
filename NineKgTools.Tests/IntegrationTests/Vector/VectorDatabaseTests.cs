using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Services.Vectors;
using Xunit;
using Xunit.Abstractions;
using MatchType = NineKgTools.Core.Models.Tags.MatchType;

namespace NineKgTools.Tests;

/// <summary>
/// 向量数据库功能测试
/// </summary>
public class VectorDatabaseTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private ServiceProvider _serviceProvider = null!;
    private MediaDbContext _dbContext = null!;
    private TagService _tagService = null!;
    private TagMatchingService _matchingService = null!;
    private Config _config = null!;

    public VectorDatabaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        // 创建服务容器
        var services = new ServiceCollection();
        
        // 添加日志
        services.AddLogging();
        
        // 添加内存缓存
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
        });
        
        // 添加配置
        _config = new Config();
        await _config.InitConfig();
        services.AddSingleton(_config);
        
        // 添加数据库上下文（使用内存数据库）
        services.AddDbContext<MediaDbContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
        
        // 添加向量数据库服务（如果启用）
        if (_config.Ai?.UseAi == true && _config.Ai?.Vector?.Enable == true && _config.Ai?.Vector?.Db != null)
        {
            services.AddSingleton(sp =>
            {
                var vectorDb = new VectorService(_config.Ai.Vector.Db);
                vectorDb.InitializeAsync().GetAwaiter().GetResult();
                return vectorDb;
            });

            services.AddSingleton(sp =>
            {
                var openaiService = sp.GetRequiredService<OpenaiService>();
                var cache = sp.GetRequiredService<IMemoryCache>();
                return new VectorEmbeddingService(openaiService, cache, _config.Ai.Vector.Db);
            });
        }
        
        // 添加服务
        services.AddScoped<OpenaiService>();
        services.AddScoped<TagMappingService>();
        services.AddScoped<TagMatchingService>();
        services.AddScoped<TagService>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        // 获取服务实例
        _dbContext = _serviceProvider.GetRequiredService<MediaDbContext>();
        _tagService = _serviceProvider.GetRequiredService<TagService>();
        _matchingService = _serviceProvider.GetRequiredService<TagMatchingService>();
        
        // 确保数据库已创建
        await _dbContext.Database.EnsureCreatedAsync();
        
        // 添加测试数据
        await InitializeTestData();
    }

    private async Task InitializeTestData()
    {
        // 创建顶级标签
        var genreTopTag = new TopTag { Id = 1, Name = "类型" };
        var styleTopTag = new TopTag { Id = 2, Name = "风格" };
        
        await _dbContext.TopTags.AddRangeAsync(genreTopTag, styleTopTag);
        await _dbContext.SaveChangesAsync();
        
        // 创建测试标签
        var tags = new[]
        {
            new Tag { Name = "动作游戏", Description = "以动作为主要玩法的游戏", TopTag = genreTopTag },
            new Tag { Name = "角色扮演", Description = "扮演角色进行冒险的游戏", TopTag = genreTopTag },
            new Tag { Name = "恐怖游戏", Description = "营造恐怖氛围的游戏", TopTag = genreTopTag },
            new Tag { Name = "像素风格", Description = "使用像素艺术的视觉风格", TopTag = styleTopTag },
            new Tag { Name = "写实风格", Description = "追求真实感的视觉表现", TopTag = styleTopTag }
        };
        
        foreach (var tag in tags)
        {
            await _tagService.AddTagAsync(tag);
        }
        
        _output.WriteLine($"初始化了 {tags.Length} 个测试标签");
    }

    [Fact(Skip = "需要配置OpenAI API")]
    public async Task TestVectorMatching_ShouldFindSimilarTags()
    {
        // 跳过测试如果未启用向量存储
        if (_config.Ai?.UseAi != true || _config.Ai?.Vector?.Enable != true || _config.Ai?.Vector?.Tag?.Enable != true)
        {
            _output.WriteLine("向量存储或向量匹配未启用，跳过测试");
            return;
        }
        
        // 测试相似查询
        var testQueries = new[]
        {
            ("动作类游戏", "动作游戏"),
            ("RPG游戏", "角色扮演"),
            ("恐怖惊悚", "恐怖游戏"),
            ("8位像素", "像素风格"),
            ("真实画面", "写实风格")
        };
        
        foreach (var (query, expectedTag) in testQueries)
        {
            var result = await _matchingService.FindBestMatchAsync(query);
            
            Assert.NotNull(result);
            _output.WriteLine($"查询: {query} -> 匹配: {result.Tag?.Name} (置信度: {result.Confidence:F2}, 类型: {result.MatchType})");
            
            // 验证是否匹配到预期的标签
            if (result.MatchType == MatchType.Vector)
            {
                Assert.Equal(expectedTag, result.Tag?.Name);
                Assert.True(result.Confidence >= 0.7, $"置信度 {result.Confidence} 应该 >= 0.7");
            }
        }
    }

    [Fact]
    public async Task TestFuzzyMatching_ShouldWorkWithoutVector()
    {
        // 测试不依赖向量的模糊匹配
        var result = await _matchingService.FindBestMatchAsync("动作游戏");
        
        Assert.NotNull(result);
        Assert.Equal("动作游戏", result.Tag?.Name);
        Assert.Equal(MatchType.Exact, result.MatchType);
        Assert.Equal(1.0, result.Confidence);
        
        _output.WriteLine($"精确匹配测试通过: {result.Tag?.Name}");
    }

    [Fact]
    public async Task TestTagService_ShouldStoreAndRetrieveTags()
    {
        // 测试标签服务基本功能
        var allTags = await _tagService.GetAllTagsAsync();
        
        Assert.NotEmpty(allTags);
        _output.WriteLine($"共有 {allTags.Count} 个标签");
        
        foreach (var tag in allTags)
        {
            _output.WriteLine($"- {tag.Name} ({tag.TopTag?.Name}): {tag.Description}");
        }
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }
}