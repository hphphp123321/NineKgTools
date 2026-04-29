using Microsoft.Extensions.Caching.Memory;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Configs;
using Serilog;
using System.Numerics.Tensors;
using System.Security.Cryptography;
using System.Text;

namespace NineKgTools.Core.Services.Vectors;

/// <summary>
/// 向量嵌入服务
/// </summary>
public class VectorEmbeddingService
{
    private readonly OpenaiService _openaiService;
    private readonly IMemoryCache _cache;
    private readonly VectorDbConfig _config;

    public VectorEmbeddingService(
        OpenaiService openaiService,
        IMemoryCache cache,
        VectorDbConfig config)
    {
        _openaiService = openaiService;
        _cache = cache;
        _config = config;
    }

    /// <summary>
    /// 生成文本的向量嵌入
    /// </summary>
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"embedding:{GetHash(text)}";

        if (_cache.TryGetValue<float[]>(cacheKey, out var cached))
        {
            Log.Debug("缓存命中: {Key}", cacheKey);
            return new ReadOnlyMemory<float>(cached);
        }

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // 使用 OpenAI 服务生成嵌入向量
            var embedding = await _openaiService.Embed(text, cancellationToken)!;
            if (embedding == null)
            {
                Log.Warning("生成嵌入向量失败，返回空向量");
                return new ReadOnlyMemory<float>(new float[_config.Dimension]);
            }

            // 转换为 float 数组
            var floatArray = embedding.Select(d => (float)d).ToArray();

            // 缓存结果（24小时）
            var cacheOptions = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromHours(24),
                Size = 1
            };
            _cache.Set(cacheKey, floatArray, cacheOptions);

            Log.Debug("成功为{Text}生成嵌入向量，维度: {Dimension}",text, floatArray.Length);
            return new ReadOnlyMemory<float>(floatArray);
        }
        catch (OperationCanceledException)
        {
            Log.Information("生成嵌入向量操作已被取消: {Text}", text);
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "生成嵌入向量失败: {Text}", text);
            }
            // 返回空向量而不是抛出异常，允许系统降级
            return new ReadOnlyMemory<float>(new float[_config.Dimension]);
        }
    }

    /// <summary>
    /// 批量生成向量嵌入
    /// </summary>
    public async Task<Dictionary<string, ReadOnlyMemory<float>>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, ReadOnlyMemory<float>>();
        var uncachedTexts = new List<string>();

        // 检查缓存
        foreach (var text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cacheKey = $"embedding:{GetHash(text)}";
            if (_cache.TryGetValue<float[]>(cacheKey, out var cached))
            {
                results[text] = new ReadOnlyMemory<float>(cached);
            }
            else
            {
                uncachedTexts.Add(text);
            }
        }

        // 批量生成未缓存的
        if (uncachedTexts.Any())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // OpenAI 支持批量嵌入
                var embeddings = await _openaiService.Embed(uncachedTexts, cancellationToken);
                if (embeddings != null)
                {
                    for (int i = 0; i < uncachedTexts.Count && i < embeddings.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var text = uncachedTexts[i];
                        var floatArray = embeddings[i].Select(d => (float)d).ToArray();

                        // 缓存
                        var cacheKey = $"embedding:{GetHash(text)}";
                        var cacheOptions = new MemoryCacheEntryOptions
                        {
                            SlidingExpiration = TimeSpan.FromHours(24),
                            Size = 1
                        };
                        _cache.Set(cacheKey, floatArray, cacheOptions);

                        results[text] = new ReadOnlyMemory<float>(floatArray);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information("批量生成嵌入向量操作已被取消");
                throw;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    Log.Error(ex, "批量生成嵌入向量失败");
                }
                // 为失败的文本生成空向量
                foreach (var text in uncachedTexts)
                {
                    if (!results.ContainsKey(text))
                    {
                        results[text] = new ReadOnlyMemory<float>(new float[_config.Dimension]);
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// 计算两个向量的相似度
    /// </summary>
    public double CalculateSimilarity(ReadOnlyMemory<float> vector1, ReadOnlyMemory<float> vector2)
    {
        if (vector1.Length != vector2.Length)
        {
            Log.Warning("向量维度不匹配: {Dim1} vs {Dim2}", vector1.Length, vector2.Length);
            return 0;
        }

        try
        {
            // 使用余弦相似度
            return TensorPrimitives.CosineSimilarity(vector1.Span, vector2.Span);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "计算向量相似度失败");
            return 0;
        }
    }

    /// <summary>
    /// 获取向量维度
    /// </summary>
    public int GetEmbeddingDimension() => _config.Dimension;

    /// <summary>
    /// 测试嵌入服务连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var testEmbedding = await GenerateEmbeddingAsync("test", cancellationToken);
            return testEmbedding.Length == _config.Dimension;
        }
        catch
        {
            return false;
        }
    }

    private string GetHash(string text)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}