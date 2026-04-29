using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Vectors;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Core.Services.Tags;

/// <summary>
/// 标签模糊匹配服务
/// </summary>
public class TagMatchingService
{
    private readonly MediaDbContext _context;
    private readonly Config _config;
    private readonly TagMappingService _mappingService;
    private readonly VectorService? _vectorDb;
    private readonly VectorEmbeddingService? _embeddingService;
    
    public TagMatchingService(MediaDbContext context, Config config, TagMappingService mappingService,
        VectorService? vectorDb = null, VectorEmbeddingService? embeddingService = null)
    {
        _context = context;
        _config = config;
        _mappingService = mappingService;
        _vectorDb = vectorDb;
        _embeddingService = embeddingService;
    }
    
    /// <summary>
    /// 查找最佳匹配的标签
    /// </summary>
    public async Task<TagMatchResult?> FindBestMatchAsync(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;
        
        // 1. 最优先：检查TagMapping表中是否有映射
        var mapping = await _mappingService.GetMappingBySourceAsync(tagName);
        if (mapping is { IsActive: true, TargetTag: not null })
        {
            // 记录映射命中
            await _mappingService.RecordMappingHitAsync(mapping.Id);
            
            return new TagMatchResult
            {
                Tag = mapping.TargetTag,
                Confidence = 1.0,
                MatchType = Models.Tags.MatchType.UserMapping,
                OriginalQuery = tagName,
                MatchDetails = $"用户映射: {mapping.Description ?? "无描述"}"
            };
        }
        
        // 2. 第二优先：精确匹配
        var exactTag = await _context.Tags.Include(t => t.TopTag)
            .FirstOrDefaultAsync(t => t.Name == tagName);
        if (exactTag != null)
        {
            return new TagMatchResult 
            { 
                Tag = exactTag, 
                Confidence = 1.0, 
                MatchType = Models.Tags.MatchType.Exact, 
                OriginalQuery = tagName 
            };
        }
        
        // 3. 第三优先：如果启用模糊匹配，执行多级匹配
        if (!_config.TagMatching.EnableFuzzyMatching)
        {
            return null;
        }
        
        var result = await PerformMultiLevelMatchingAsync(tagName);
        
        // 如果找到匹配结果且置信度足够高，自动创建映射
        if (result != null && result.Tag != null && result.Confidence >= 0.8 && result.MatchType != Core.Models.Tags.MatchType.UserMapping)
        {
            try
            {
                await _mappingService.AddMappingAsync(
                    tagName, 
                    result.Tag.Id, 
                    $"自动生成 - {result.MatchType} (置信度: {result.Confidence:F2})"
                );
                Log.Information("自动创建标签映射: {Source} -> {Target} (置信度: {Confidence})", 
                    tagName, result.Tag.Name, result.Confidence);
            }
            catch (InvalidOperationException)
            {
                // 映射已存在，忽略
            }
            catch (Exception ex)
            {
                Log.Warning("自动创建标签映射失败: {Error}", ex.Message);
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 查找所有可能的匹配
    /// </summary>
    public async Task<List<TagMatchResult>> FindAllMatchesAsync(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return new List<TagMatchResult>();
            
        var results = new List<TagMatchResult>();
        
        // 获取所有标签（用于模糊匹配）
        var allTags = await _context.Tags.Include(t => t.TopTag).ToListAsync();
        
        // 1. 精确匹配
        var exactMatches = allTags.Where(t => t.Name == tagName).ToList();
        results.AddRange(exactMatches.Select(t => new TagMatchResult
        {
            Tag = t,
            Confidence = 1.0,
            MatchType = Core.Models.Tags.MatchType.Exact,
            OriginalQuery = tagName
        }));
        
        // 如果找到精确匹配，直接返回
        if (results.Any())
            return results;
            
        // 如果未启用模糊匹配，返回空结果
        if (!_config.TagMatching.EnableFuzzyMatching)
            return results;
        
        // 2. 规范化匹配
        if (_config.TagMatching.EnableNormalizedMatching)
        {
            var normalizedQuery = NormalizeTagName(tagName);
            var normalizedMatches = allTags
                .Where(t => NormalizeTagName(t.Name) == normalizedQuery)
                .Select(t => new TagMatchResult
                {
                    Tag = t,
                    Confidence = 0.95,
                    MatchType = Core.Models.Tags.MatchType.Normalized,
                    OriginalQuery = tagName,
                    MatchDetails = $"规范化匹配: {normalizedQuery}"
                });
            results.AddRange(normalizedMatches);
        }
        
        // 3. 包含匹配
        if (_config.TagMatching.EnableContainsMatching)
        {
            var containsMatches = allTags
                .Where(t => t.Name.Contains(tagName) || tagName.Contains(t.Name))
                .Select(t => new TagMatchResult
                {
                    Tag = t,
                    Confidence = CalculateContainsConfidence(t.Name, tagName),
                    MatchType = Core.Models.Tags.MatchType.Contains,
                    OriginalQuery = tagName,
                    MatchDetails = $"包含匹配"
                });
            results.AddRange(containsMatches);
        }
        
        // 4. 相似度匹配
        var similarityResults = await PerformSimilarityMatchingAsync(tagName, allTags);
        results.AddRange(similarityResults);
        
        // 去重并按置信度排序
        var uniqueResults = results
            .GroupBy(r => r.Tag?.Id)
            .Select(g => g.OrderByDescending(r => r.Confidence).First())
            .OrderByDescending(r => r.Confidence)
            .Take(_config.TagMatching.MaxMatchResults)
            .ToList();
        
        if (_config.TagMatching.LogMatchDetails && uniqueResults.Any())
        {
            Log.Debug("标签匹配结果 '{Query}': {Results}", 
                tagName, 
                string.Join(", ", uniqueResults.Select(r => $"{r.Tag?.Name}({r.Confidence:F2})")));
        }
        
        return uniqueResults;
    }
    
    /// <summary>
    /// 执行多级匹配（仅在启用模糊匹配时调用）
    /// </summary>
    private async Task<TagMatchResult?> PerformMultiLevelMatchingAsync(string tagName)
    {
        // 1. 先尝试向量匹配（如果启用）- 优先级最高，因为语义匹配更准确
        if (_config?.Ai?.UseAi == true &&
            _config?.Ai?.Vector?.Enable == true &&
            _config?.Ai?.Vector?.Tag?.Enable == true &&
            _vectorDb != null &&
            _embeddingService != null)
        {
            var vectorResult = await PerformVectorMatchingAsync(tagName);
            if (vectorResult != null)
            {
                Log.Debug("向量匹配成功: {Query} -> {Tag} (相似度: {Similarity})", 
                    tagName, vectorResult.Tag?.Name, vectorResult.Confidence);
                return vectorResult;
            }
        }
        
        // 获取所有标签用于匹配
        var allTags = await _context.Tags.Include(t => t.TopTag).ToListAsync();
        
        // 2. 规范化匹配
        if (_config.TagMatching.EnableNormalizedMatching)
        {
            var normalizedQuery = NormalizeTagName(tagName);
            var normalizedTag = allTags.FirstOrDefault(t => NormalizeTagName(t.Name) == normalizedQuery);
            if (normalizedTag != null)
            {
                return new TagMatchResult 
                { 
                    Tag = normalizedTag, 
                    Confidence = 0.95, 
                    MatchType = Models.Tags.MatchType.Normalized, 
                    OriginalQuery = tagName,
                    MatchDetails = _config.TagMatching.LogMatchDetails ? $"规范化匹配: {normalizedQuery}" : null
                };
            }
        }
        
        // 3. 包含匹配
        if (_config.TagMatching.EnableContainsMatching)
        {
            var containsTag = allTags
                .Where(t => t.Name.Contains(tagName) || tagName.Contains(t.Name))
                .OrderByDescending(t => CalculateContainsConfidence(t.Name, tagName))
                .FirstOrDefault();
            
            if (containsTag != null)
            {
                var confidence = CalculateContainsConfidence(containsTag.Name, tagName);
                if (confidence >= 0.6) // 只有足够高的包含匹配才返回
                {
                    return new TagMatchResult
                    {
                        Tag = containsTag,
                        Confidence = confidence,
                        MatchType = Models.Tags.MatchType.Contains,
                        OriginalQuery = tagName,
                        MatchDetails = _config.TagMatching.LogMatchDetails ? $"包含匹配" : null
                    };
                }
            }
        }
        
        // 4. 相似度匹配
        var similarityResults = await PerformSimilarityMatchingAsync(tagName, allTags);
        var bestSimilarity = similarityResults.FirstOrDefault();
        if (bestSimilarity != null && bestSimilarity.Confidence >= _config.TagMatching.SimilarityThreshold)
        {
            return bestSimilarity;
        }
        
        // 如果有任何相似度结果，返回最佳的
        return bestSimilarity;
    }
    
    /// <summary>
    /// 执行相似度匹配
    /// </summary>
    private async Task<List<TagMatchResult>> PerformSimilarityMatchingAsync(string tagName, List<Tag>? allTags = null)
    {
        var results = new List<TagMatchResult>();
        
        // 如果没有提供标签列表，从数据库获取
        if (allTags == null)
        {
            allTags = await _context.Tags.Include(t => t.TopTag).ToListAsync();
        }
        
        foreach (var tag in allTags)
        {
            var similarity = StringSimilarityCalculator.GetAverageSimilarity(tagName, tag.Name);
            if (similarity >= _config.TagMatching.SimilarityThreshold)
            {
                results.Add(new TagMatchResult
                {
                    Tag = tag,
                    Confidence = similarity,
                    MatchType = Models.Tags.MatchType.Similarity,
                    OriginalQuery = tagName,
                    MatchDetails = _config.TagMatching.LogMatchDetails ? $"相似度: {similarity:F3}" : null
                });
            }
        }
        
        return results.OrderByDescending(r => r.Confidence).ToList();
    }
    
    /// <summary>
    /// 规范化标签名
    /// </summary>
    private string NormalizeTagName(string name)
    {
        // 转换为小写
        name = name.ToLowerInvariant();
        
        // 移除特殊字符，保留中文、日文、英文字符
        name = Regex.Replace(name, @"[^a-zA-Z0-9\u4e00-\u9fa5\u3040-\u309F\u30A0-\u30FF]", "");
        
        // 移除多余空格
        name = Regex.Replace(name, @"\s+", " ").Trim();
        
        return name;
    }
    
    /// <summary>
    /// 计算包含匹配的置信度
    /// </summary>
    private double CalculateContainsConfidence(string tagName, string query)
    {
        // 如果完全包含，根据长度比例计算置信度
        if (tagName == query)
            return 1.0;
        if (tagName.Contains(query))
            return 0.7 + (0.2 * ((double)query.Length / tagName.Length));
        if (query.Contains(tagName))
            return 0.7 + (0.2 * ((double)tagName.Length / query.Length));
        return 0.0;
    }

    /// <summary>
    /// 执行向量匹配
    /// </summary>
    private async Task<TagMatchResult?> PerformVectorMatchingAsync(string tagName)
    {
        if (_vectorDb == null || _embeddingService == null)
        {
            return null;
        }

        try
        {
            // 生成查询文本的向量
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(tagName);
            
            // 在向量数据库中搜索相似标签
            var searchResults = await _vectorDb.SearchTagsAsync(
                queryEmbedding,
                topK: _config?.Ai?.Vector?.Tag?.SearchTopK ?? 3,
                threshold: _config?.Ai?.Vector?.Tag?.SimilarityThreshold ?? 0.05
            );

            if (!searchResults.Any())
            {
                return null;
            }

            // 获取最佳匹配结果
            var bestResult = searchResults.First();

            var tagId = bestResult.Record.TagId;
            
            // 从数据库获取完整的标签信息
            var tag = await _context.Tags
                .Include(t => t.TopTag)
                .FirstOrDefaultAsync(t => t.Id == tagId);

            if (tag == null)
            {
                Log.Warning("向量匹配找到标签ID {TagId}，但数据库中不存在", tagId);
                return null;
            }

            return new TagMatchResult
            {
                Tag = tag,
                Confidence = bestResult.Score,
                MatchType = Models.Tags.MatchType.Vector,
                OriginalQuery = tagName,
                MatchDetails = _config.TagMatching.LogMatchDetails 
                    ? $"向量匹配 - 相似度: {bestResult.Score:F3}" 
                    : null
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "执行向量匹配失败: {Query}", tagName);
            return null;
        }
    }
}