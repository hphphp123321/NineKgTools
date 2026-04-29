using System.Text.Json;

namespace NineKgTools.Core.Models.Vectors;

/// <summary>
/// 向量记录实体
/// </summary>
public class VectorRecord
{
    /// <summary>
    /// 记录ID（格式: CollectionName_EntityId）
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 集合名称（如 "Tags", "Media" 等）
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// 原始文本
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 向量数据（JSON序列化）
    /// </summary>
    public string EmbeddingJson { get; set; } = string.Empty;

    /// <summary>
    /// 向量维度
    /// </summary>
    public int Dimension { get; set; }

    /// <summary>
    /// 元数据（JSON序列化）
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// 获取向量数据
    /// </summary>
    public ReadOnlyMemory<float> GetEmbedding()
    {
        var floats = JsonSerializer.Deserialize<float[]>(EmbeddingJson) 
            ?? Array.Empty<float>();
        return new ReadOnlyMemory<float>(floats);
    }

    /// <summary>
    /// 设置向量数据
    /// </summary>
    public void SetEmbedding(ReadOnlyMemory<float> embedding)
    {
        EmbeddingJson = JsonSerializer.Serialize(embedding.ToArray());
        Dimension = embedding.Length;
    }

    /// <summary>
    /// 获取元数据
    /// </summary>
    public Dictionary<string, object> GetMetadata()
    {
        if (string.IsNullOrEmpty(MetadataJson))
            return new Dictionary<string, object>();
            
        return JsonSerializer.Deserialize<Dictionary<string, object>>(MetadataJson) 
            ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// 设置元数据
    /// </summary>
    public void SetMetadata(Dictionary<string, object>? metadata)
    {
        MetadataJson = metadata != null 
            ? JsonSerializer.Serialize(metadata) 
            : null;
    }
}