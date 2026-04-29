using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using NineKgTools.Core.Models.Vectors;
using Serilog;

namespace NineKgTools.Core.Services.Vectors;

/// <summary>
/// VectorService - 媒体相关功能
/// </summary>
public partial class VectorService
{
    /// <summary>
    /// 添加媒体向量记录
    /// </summary>
    public async Task<string> AddMediaVectorAsync(MediaVector mediaVector, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        // 如果没有ID，生成一个
        if (string.IsNullOrEmpty(mediaVector.Id))
        {
            mediaVector.Id = GenerateId("media");
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _mediaCollection!.UpsertAsync(mediaVector, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("添加媒体向量记录: {Id} - {Title}", mediaVector.Id, mediaVector.MediaTitle);

        return mediaVector.Id;
    }

    /// <summary>
    /// 更新媒体向量记录
    /// </summary>
    public async Task UpdateMediaVectorAsync(MediaVector mediaVector, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _mediaCollection!.UpsertAsync(mediaVector, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("更新媒体向量记录: {Id} - {Title}", mediaVector.Id, mediaVector.MediaTitle);
    }

    /// <summary>
    /// 删除媒体向量记录
    /// </summary>
    public async Task DeleteMediaVectorAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _mediaCollection!.DeleteAsync(id, cancellationToken: cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("删除媒体向量记录: {Id}", id);
    }

    /// <summary>
    /// 检查媒体记录是否存在
    /// </summary>
    public async Task<bool> ExistsMediaAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        try
        {
            var record = await _mediaCollection!.GetAsync(id, cancellationToken: cancellationToken);
            return record != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 媒体向量搜索
    /// </summary>
    public async Task<List<Models.Vectors.VectorSearchResult<MediaVector>>> SearchMediaAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int topK = 10,
        double threshold = 0.05,
        CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        var results = new List<Models.Vectors.VectorSearchResult<MediaVector>>();

        var vectorSearchOptions = new VectorSearchOptions<MediaVector>
        {
            VectorProperty = r => r.Embedding,
        };

        try
        {
            // 执行向量搜索
            var searchResults =
                _mediaCollection!.SearchAsync(queryEmbedding, top: topK, vectorSearchOptions, cancellationToken: cancellationToken);

            var totalScanned = 0;
            await foreach (var result in searchResults)
            {
                cancellationToken.ThrowIfCancellationRequested();
                totalScanned++;

                // Score 可能为 null，需要处理
                if (!result.Score.HasValue || result.Score >= threshold)
                {
                    results.Add(new Models.Vectors.VectorSearchResult<MediaVector>(result.Record, result.Score ?? 1.0));
                }
            }

            // 汇总日志：把每条结果逐行 Debug 改成一行摘要，避免搜索时刷屏
            Log.Debug("媒体向量搜索完成: 扫描 {Scanned} 条，通过阈值 {Threshold} 保留 {Accepted} 条",
                totalScanned, threshold, results.Count);

            return results;
        }
        catch (OperationCanceledException)
        {
            Log.Debug("媒体向量搜索操作已被取消");
            throw;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "媒体向量搜索失败");
            }
        }

        return results;
    }


    /// <summary>
    /// 获取媒体集合中的记录数
    /// </summary>
    public async Task<int> GetMediaCountAsync(CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        try
        {
            // 暂时不支持
            Log.Warning("GetMediaCountAsync 暂时不支持");
            return -1;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning(ex, "获取媒体集合计数失败");
            }
            return -1;
        }
    }

    /// <summary>
    /// 批量添加媒体向量记录
    /// </summary>
    public async Task<List<string>> AddBatchMediaVectorsAsync(List<MediaVector> records, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        var ids = new List<string>();

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(record.Id))
            {
                record.Id = GenerateId("media");
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
                await _mediaCollection!.UpsertAsync(record, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            _writeLock.Release();
        }

        Log.Debug("批量添加 {Count} 个媒体向量记录", records.Count);

        return ids;
    }

    /// <summary>
    /// 通过媒体ID获取向量记录
    /// </summary>
    public async Task<MediaVector?> GetMediaVectorByMediaIdAsync(int mediaId, CancellationToken cancellationToken = default)
    {
        if (_mediaCollection == null)
        {
            await InitializeAsync(cancellationToken);
        }

        var id = $"media_{mediaId}";

        try
        {
            var record = await _mediaCollection!.GetAsync(id, cancellationToken: cancellationToken);
            return record;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Warning(ex, "获取媒体向量记录失败: MediaId={MediaId}", mediaId);
            }
            return null;
        }
    }
}