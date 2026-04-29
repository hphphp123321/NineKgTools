using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using NineKgTools.Core.Models.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Vectors;

/// <summary>
/// VectorService - 标签相关功能
/// </summary>
public partial class VectorService
{
    /// <summary>
    /// 添加标签向量记录
    /// </summary>
    public async Task<string> AddTagVectorAsync(TagVector tagVector, CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        // 如果没有ID，生成一个
        if (string.IsNullOrEmpty(tagVector.Id))
        {
            tagVector.Id = GenerateId("tag");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _tagCollection!.UpsertAsync(tagVector, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("添加标签向量记录: {Id} - {Name}", tagVector.Id, tagVector.TagName);

        return tagVector.Id;
    }

    /// <summary>
    /// 更新标签向量记录
    /// </summary>
    public async Task UpdateTagVectorAsync(TagVector tagVector, CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _tagCollection!.UpsertAsync(tagVector, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("更新标签向量记录: {Id} - {Name}", tagVector.Id, tagVector.TagName);
    }

    /// <summary>
    /// 删除标签向量记录
    /// </summary>
    public async Task DeleteTagVectorAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _tagCollection!.DeleteAsync(id, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("删除标签向量记录: {Id}", id);
    }

    /// <summary>
    /// 检查标签记录是否存在
    /// </summary>
    public async Task<bool> ExistsTagAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        try
        {
            var record = await _tagCollection!.GetAsync(id, cancellationToken: cancellationToken);
            return record != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 标签向量搜索
    /// </summary>
    public async Task<List<Models.Vectors.VectorSearchResult<TagVector>>> SearchTagsAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 10,
        double threshold = 0.05,
        CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        var results = new List<Models.Vectors.VectorSearchResult<TagVector>>();

        var vectorSearchOptions = new VectorSearchOptions<TagVector>
        {
            VectorProperty = r => r.Embedding,
        };

        try
        {
            // 执行向量搜索
            var searchResults =
                _tagCollection!.SearchAsync(queryEmbedding, top: topK, vectorSearchOptions, cancellationToken: cancellationToken);

            var totalScanned = 0;
            await foreach (var result in searchResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalScanned++;

                // Score 可能为 null，需要处理
                if (!result.Score.HasValue || result.Score >= threshold)
                {
                    results.Add(new Models.Vectors.VectorSearchResult<TagVector>(result.Record, result.Score ?? 1.0));
                }
            }

            // 汇总日志：把每条结果逐行 Debug 改成一行摘要，避免搜索时刷屏
            Log.Debug("标签向量搜索完成: 扫描 {Scanned} 条，通过阈值 {Threshold} 保留 {Accepted} 条",
                totalScanned, threshold, results.Count);

            return results;
        }
        catch (OperationCanceledException)
        {
            Log.Debug("标签向量搜索操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "标签向量搜索失败");
            }
        }

        return results;
    }

    /// <summary>
    /// 获取标签集合中的记录数
    /// </summary>
    public async Task<int> GetTagCountAsync(CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        try
        {
            // 暂时不支持
            Log.Warning("GetTagCountAsync 暂时不支持");
            return -1;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning(ex, "获取标签集合计数失败");
            }
            return -1;
        }
    }

    /// <summary>
    /// 批量添加标签向量记录
    /// </summary>
    public async Task<List<string>> AddBatchTagVectorsAsync(List<TagVector> records, CancellationToken cancellationToken = default)
    {
        if (_tagCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        var ids = new List<string>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(record.Id))
            {
                record.Id = GenerateId("tag");
            }
            ids.Add(record.Id);
        }

        // 批量插入（持有写锁期间完成所有写入，减少锁竞争）
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _tagCollection!.UpsertAsync(record, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("批量添加 {Count} 个标签向量记录", records.Count);

        return ids;
    }
}