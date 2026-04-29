using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Models.Vectors;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Vectors;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NineKgTools.Core.Services.Tags;

public class TagService
{
    public readonly MediaDbContext _context;
    private readonly TagMatchingService? _matchingService;
    private readonly VectorService? _vectorDb;
    private readonly VectorEmbeddingService? _embeddingService;
    private readonly Config? _config;
    
    public TagService(MediaDbContext context, TagMatchingService? matchingService = null,
        VectorService? vectorDb = null, VectorEmbeddingService? embeddingService = null,
        Config? config = null)
    {
        _context = context;
        _matchingService = matchingService;
        _vectorDb = vectorDb;
        _embeddingService = embeddingService;
        _config = config;
    }
    
    public Tag? GetTagById(int id)
    {
        return _context.Tags
            .Include(t => t.Medias)
            .Include(t => t.TopTag)
            .FirstOrDefault(x => x.Id == id);
    }
    
    /// <summary>
    /// 根据标签名获取标签
    /// </summary>
    /// <param name="name">标签名</param>
    /// <param name="useFuzzyMatching">是否使用模糊匹配（默认使用）</param>
    /// <returns>匹配到的标签</returns>
    public Tag? GetTagByName(string name, bool useFuzzyMatching = true)
    {
        // 如果没有匹配服务或不使用模糊匹配，使用精确匹配
        if (_matchingService == null || !useFuzzyMatching)
        {
            var tag = _context.Tags.FirstOrDefault(x => x.Name == name);
            return tag;
        }
        
        // 使用模糊匹配服务
        var matchResult = _matchingService.FindBestMatchAsync(name).GetAwaiter().GetResult();
        return matchResult?.Tag;
    }
    
    /// <summary>
    /// 根据标签名获取标签（异步版本）
    /// </summary>
    /// <param name="name">标签名</param>
    /// <param name="useFuzzyMatching">是否使用模糊匹配（默认使用）</param>
    /// <returns>匹配到的标签</returns>
    public async Task<Tag?> GetTagByNameAsync(string name, bool useFuzzyMatching = true)
    {
        // 如果没有匹配服务或不使用模糊匹配，使用精确匹配
        if (_matchingService == null || !useFuzzyMatching)
        {
            var tag = await _context.Tags.FirstOrDefaultAsync(x => x.Name == name);
            return tag;
        }
        
        // 使用模糊匹配服务
        var matchResult = await _matchingService.FindBestMatchAsync(name);
        return matchResult?.Tag;
    }
    
    /// <summary>
    /// 获取所有可能的标签匹配
    /// </summary>
    /// <param name="name">标签名</param>
    /// <returns>所有匹配结果</returns>
    public async Task<List<TagMatchResult>> GetAllMatchingTagsAsync(string name)
    {
        if (_matchingService == null)
        {
            // 如果没有匹配服务，返回精确匹配结果
            var tag = await _context.Tags.FirstOrDefaultAsync(x => x.Name == name);
            if (tag != null)
            {
                return new List<TagMatchResult>
                {
                    new TagMatchResult 
                    { 
                        Tag = tag, 
                        Confidence = 1.0, 
                        MatchType = Models.Tags.MatchType.Exact,
                        OriginalQuery = name
                    }
                };
            }
            return new List<TagMatchResult>();
        }
        
        return await _matchingService.FindAllMatchesAsync(name);
    }
    
    /// <summary>
    /// 获取所有的顶级标签复制体，不包含标签以及相关媒体
    /// </summary>
    public async Task<List<TopTag>> GetCopiedTopTagsAsync()
    {
        var topTags = await _context.TopTags.ToListAsync();
        return topTags.Copy();
    }
    
    /// <summary>
    /// 获取一个顶级标签包含的标签以及相关媒体
    /// </summary>
    /// <param name="topTagId"></param>
    /// <returns></returns>
    public async Task<List<Tag>> GetTagsByTopTagIdAsync(int topTagId)
    {
        var topTag = await _context.TopTags
            .Include(t => t.Tags)
            .ThenInclude(t => t.Medias)
            .FirstOrDefaultAsync(t => t.Id == topTagId);
        
        return topTag == null ? [] : topTag.Tags;
    }
    
    /// <summary>
    /// 添加一个新的顶层标签
    /// </summary>
    public async Task<TopTag> AddTopTagAsync(TopTag topTag)
    {
        await _context.TopTags.AddAsync(topTag);
        await _context.SaveChangesAsync();

        Log.Information("添加新的顶层标签：{TopTagName}", topTag.Name);
        return topTag;
    }

    /// <summary>
    /// 删除一个顶层标签
    /// </summary>
    public async Task RemoveTopTagAsync(TopTag topTag)
    {
        var dbTopTag = await _context.TopTags.FindAsync(topTag.Id);
        if (dbTopTag == null)
        {
            Log.Warning("删除顶层标签失败，未找到对应标签：{TopTagName}", topTag.Name);
            return;
        }

        _context.TopTags.Remove(dbTopTag);
        await _context.SaveChangesAsync();
        
        Log.Information("删除顶层标签：{TopTagName}", topTag.Name);
    }

    /// <summary>
    /// 更新一个顶层标签
    /// </summary>
    public async Task UpdateTopTagAsync(TopTag topTag)
    {
        var dbTopTag = await _context.TopTags.FindAsync(topTag.Id);
        if (dbTopTag == null)
        {
            Log.Warning("更新顶层标签失败，未找到对应标签：{TopTagName}", topTag.Name);
            return;
        }

        dbTopTag.Name = topTag.Name;

        await _context.SaveChangesAsync();

        Log.Information("更新顶层标签：{TopTagName}", topTag.Name);
    }

    /// <summary>
    /// 添加一个标签
    /// </summary>
    /// <param name="tag">需要新增标签</param>
    public async Task<Tag> AddTagAsync(Tag tag)
    {
        // 找到对应的数据库顶层标签
        var dbTopTag = await _context.TopTags.FindAsync(tag.TopTag.Id);
        if (dbTopTag == null)
        {
            Log.Warning("添加标签失败，未找到对应的顶层标签：{TopTagName}", tag.TopTag.Name);
            return tag;
        }

        tag.TopTag = dbTopTag;

        await _context.Tags.AddAsync(tag);
        await _context.SaveChangesAsync();

        // 生成并存储向量
        if (ShouldUseVectorStorage())
        {
            await StoreTagVectorAsync(tag);
        }

        Log.Information("在顶层标签{TopTagName}下添加新的标签：{TagName}", dbTopTag.Name, tag.Name);
        return tag;
    }

    /// <summary>
    /// 删除一个标签
    /// </summary>
    public async Task RemoveTagAsync(Tag tag)
    {
        var dbTag = await _context.Tags.FindAsync(tag.Id);
        if (dbTag == null)
        {
            Log.Warning("删除标签失败，未找到对应标签：{TagName}", tag.Name);
            return;
        }

        // 删除向量
        if (ShouldUseVectorStorage())
        {
            try
            {
                await _vectorDb!.DeleteTagVectorAsync($"tag_{tag.Id}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "删除标签向量失败: {TagId}", tag.Id);
            }
        }

        _context.Tags.Remove(dbTag);
        await _context.SaveChangesAsync();

        Log.Information("删除标签：{TagName}", tag.Name);
    }

    public async Task UpdateTagAsync(Tag tag)
    {
        var dbTag = await _context.Tags.FindAsync(tag.Id);
        if (dbTag == null)
        {
            Log.Warning("更新标签失败，未找到对应标签：{TagName}", tag.Name);
            return;
        }

        dbTag.Name = tag.Name;
        dbTag.Description = tag.Description;
        dbTag.TopTag = tag.TopTag;

        await _context.SaveChangesAsync();

        // 更新向量
        if (ShouldUseVectorStorage())
        {
            var vectorId = $"tag_{tag.Id}";
            if (await _vectorDb!.ExistsTagAsync(vectorId))
            {
                await UpdateTagVectorAsync(tag);
            }
            else
            {
                await StoreTagVectorAsync(tag);
            }
        }

        Log.Information("更新标签：{TagName}", tag.Name);
    }

    /// <summary>
    /// 找到数据库中的标签
    /// </summary>
    /// <param name="tags"></param>
    public async Task<List<Tag>> FindTagsAsync(List<Tag> tags)
    {
        var dbTags = await Task.WhenAll(tags.Select(FindTagAsync));
        return dbTags.Where(t => t != null).Select(t => t!).ToList();
    }

    public async Task<Tag?> FindTagAsync(Tag tag)
    {
        // 从数据库中查找
        var dbTag = await _context.Tags
            .Include(t => t.TopTag)
        .FirstOrDefaultAsync(t => t.Name == tag.Name);

        return dbTag;
    }

    /// <summary>
    /// 获取所有标签，支持按名称搜索过滤
    /// </summary>
    /// <param name="searchTerm">搜索关键词，为空时返回所有标签</param>
    /// <returns>标签列表</returns>
    public async Task<List<Tag>> GetAllTagsAsync(string? searchTerm = null)
    {
        var query = _context.Tags.Include(t => t.TopTag).AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(t => t.Name.Contains(searchTerm) || 
                                    (t.Description != null && t.Description.Contains(searchTerm)));
        }

        return await query.OrderBy(t => t.TopTag.Name).ThenBy(t => t.Name).ToListAsync();
    }

    /// <summary>
    /// 获取媒体的所有标签，包括顶层标签信息
    /// </summary>
    /// <param name="mediaId">媒体ID</param>
    /// <returns>标签列表</returns>
    public async Task<List<Tag>> GetMediaTagsAsync(int mediaId)
    {
        return await _context.Tags
            .Include(t => t.TopTag)
            .Where(t => t.Medias.Any(m => m.Id == mediaId))
            .OrderBy(t => t.TopTag.Name)
            .ThenBy(t => t.Name)
            .ToListAsync();
    }

    /// <summary>
    /// 根据yaml文件初始化数据库
    /// </summary>
    public async Task InitializeTagsDbFromYaml()
    {
        Log.Information("正在初始化标签...");

        // 如果数据库中不存在标签
        if (!await _context.Tags.AnyAsync())
        {
            Log.Debug("数据库中未存在标签，开始读取yaml文件");

            // 从yaml文件中读取标签信息并且存入数据库
            var yamlFilePath = Config.FindConfigFile("tags.yaml");
            var tagTypes = await GetTagsFromYamlAsync(yamlFilePath);

            foreach (var topTag in tagTypes)
            {
                var dbTopTag = await _context.TopTags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == topTag.Name);
                if (dbTopTag == null)
                {
                    await _context.TopTags.AddAsync(topTag);
                }
                else
                {
                    foreach (var tag in topTag.Tags)
                    {
                        var dbTag = await _context.Tags.AsNoTracking().FirstOrDefaultAsync(t => t.Name == tag.Name);
                        if (dbTag == null)
                        {
                            dbTag = new Tag
                            {
                                Name = tag.Name,
                                Description = tag.Description,
                                TopTag = dbTopTag,
                            };
                            await _context.Tags.AddAsync(dbTag);
                        }
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        // 如果启用了向量存储，初始化所有标签的向量
        if (ShouldUseVectorStorage())
        {
            await InitializeTagVectorsAsync();
        }

        Log.Information("标签初始化完毕");
    }

    /// <summary>
    /// 初始化所有标签的向量
    /// </summary>
    private async Task InitializeTagVectorsAsync()
    {
        try
        {
            Log.Information("开始初始化标签向量...");
            
            // 获取所有标签
            var allTags = await _context.Tags
                .Include(t => t.TopTag)
                .ToListAsync();
            
            if (!allTags.Any())
            {
                Log.Information("没有找到需要向量化的标签");
                return;
            }
            
            var vectorizedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;
            
            // 批量处理标签
            var batchSize = _config?.Ai?.Vector?.Db?.BatchSize ?? 100;
            var batches = allTags.Chunk(batchSize);
            
            foreach (var batch in batches)
            {
                var tagsToVectorize = new List<Tag>();
                
                // 检查哪些标签需要向量化
                foreach (var tag in batch)
                {
                    var vectorId = $"tag_{tag.Id}";
                    if (!await _vectorDb!.ExistsTagAsync(vectorId))
                    {
                        tagsToVectorize.Add(tag);
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                
                // 向量化需要处理的标签
                foreach (var tag in tagsToVectorize)
                {
                    try
                    {
                        await StoreTagVectorAsync(tag);
                        vectorizedCount++;
                        
                        // 避免请求过快
                        if (vectorizedCount % 10 == 0)
                        {
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "向量化标签失败: {TagId} - {TagName}", tag.Id, tag.Name);
                        failedCount++;
                    }
                }
            }
            
            Log.Information("标签向量初始化完成: 新增 {Vectorized} 个, 跳过 {Skipped} 个, 失败 {Failed} 个", 
                vectorizedCount, skippedCount, failedCount);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化标签向量失败");
        }
    }

    /// <summary>
    /// 根据标签名匹配标签
    /// </summary>
    /// <param name="tag">数据库中的标签</param>
    /// <param name="target">需要匹配的标签名</param>
    /// <returns></returns>
    private bool MatchTag(Tag tag, string target)
    {
        return tag.Name == target;
    }

    private async Task<List<TopTag>> GetTagsFromYamlAsync(string yamlFilePath)
    {
        var tagTypes = new List<TopTag>();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var yamlData = await File.ReadAllTextAsync(yamlFilePath);
        var yamlTags = deserializer.Deserialize<YamlTags>(yamlData);

        foreach (var yamlTagType in yamlTags.Tags)
        {
            var tagType = new TopTag
            {
                Id = yamlTagType.Id,
                Name = yamlTagType.Name
            };

            tagTypes.Add(tagType);

            foreach (var yamlTag in yamlTagType.Tags)
            {
                var tag = new Tag(yamlTag.Id, yamlTag.Name, yamlTag.Description)
                {
                    TopTag = tagType,
                };
                tagType.Tags.Add(tag);
            }
        }

        return tagTypes;
    }

    /// <summary>
    /// 同步标签向量（供向量同步任务使用）
    /// </summary>
    public async Task SyncTagVectorAsync(Tag tag, bool forceUpdate = false)
    {
        if (!ShouldUseVectorStorage())
            return;
            
        var vectorId = $"tag_{tag.Id}";
        var exists = await _vectorDb!.ExistsTagAsync(vectorId);
        
        if (!exists)
        {
            await StoreTagVectorAsync(tag);
        }
        else if (forceUpdate)
        {
            await UpdateTagVectorAsync(tag);
        }
    }
    
    /// <summary>
    /// 判断是否应该使用向量存储
    /// </summary>
    public bool ShouldUseVectorStorage()
    {
        return _config?.Ai?.UseAi == true &&
               _config?.Ai?.Vector?.Enable == true &&
               _config?.Ai?.Vector?.Tag?.Enable == true &&
               _vectorDb != null &&
               _embeddingService != null;
    }

    /// <summary>
    /// 为标签生成并存储向量
    /// </summary>
    private async Task StoreTagVectorAsync(Tag tag)
    {
        try
        {
            var text = BuildTagText(tag);
            var embedding = await _embeddingService!.GenerateEmbeddingAsync(text);
            
            var tagVector = new TagVector
            {
                Id = $"tag_{tag.Id}",
                TagId = tag.Id,
                TagName = tag.Name,
                Text = text,
                Embedding = embedding,
                Description = tag.Description,
                TopTagName = tag.TopTag?.Name,
                TopTagId = tag.TopTag?.Id
            };
            
            await _vectorDb!.AddTagVectorAsync(tagVector);
            Log.Debug("成功存储标签向量: {Id} - {Name}", tag.Id, tag.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "存储标签向量失败: {Id} - {Name}", tag.Id, tag.Name);
        }
    }

    /// <summary>
    /// 更新标签向量
    /// </summary>
    private async Task UpdateTagVectorAsync(Tag tag)
    {
        try
        {
            var text = BuildTagText(tag);
            var embedding = await _embeddingService!.GenerateEmbeddingAsync(text);
            
            var tagVector = new TagVector
            {
                Id = $"tag_{tag.Id}",
                TagId = tag.Id,
                TagName = tag.Name,
                Text = text,
                Embedding = embedding,
                Description = tag.Description,
                TopTagName = tag.TopTag?.Name,
                TopTagId = tag.TopTag?.Id
            };
            
            await _vectorDb!.UpdateTagVectorAsync(tagVector);
            Log.Debug("成功更新标签向量: {Id} - {Name}", tag.Id, tag.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新标签向量失败: {Id} - {Name}", tag.Id, tag.Name);
        }
    }

    /// <summary>
    /// 构建标签文本用于向量化
    /// </summary>
    private string BuildTagText(Tag tag)
    {
        var parts = new List<string> { tag.Name };
        
        // 添加描述
        if (!string.IsNullOrWhiteSpace(tag.Description))
        {
            parts.Add(tag.Description);
        }
        
        // 添加顶级标签
        if (tag.TopTag != null)
        {
            parts.Add($"类别:{tag.TopTag.Name}");
        }
        
        return string.Join(" ", parts);
    }
}