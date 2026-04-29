using Microsoft.Extensions.VectorData;

namespace NineKgTools.Core.Models.Vectors;

/// <summary>
/// 向量记录基类
/// </summary>
public abstract class BaseVector
{
    /// <summary>
    /// 记录唯一标识
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 文本内容（用于生成向量的原始文本）
    /// </summary>
    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 向量嵌入（默认使用OpenAI的1536维度）
    /// </summary>
    [VectorStoreVector(Dimensions: 1536)]
    public ReadOnlyMemory<float>? Embedding { get; set; }

    /// <summary>
    /// 记录类型（如 "Tag", "Media" 等）
    /// </summary>
    [VectorStoreData]
    public string RecordType { get; set; } = string.Empty;
}