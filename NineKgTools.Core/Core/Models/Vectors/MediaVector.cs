using Microsoft.Extensions.VectorData;

namespace NineKgTools.Core.Models.Vectors;

/// <summary>
/// 媒体向量记录模型
/// </summary>
public class MediaVector : BaseVector
{
    /// <summary>
    /// 媒体ID
    /// </summary>
    [VectorStoreData]
    public int MediaId { get; set; }

    /// <summary>
    /// 媒体标题（使用基类的Text存储完整搜索文本）
    /// </summary>
    [VectorStoreData]
    public string MediaTitle { get; set; } = string.Empty;

    /// <summary>
    /// 媒体类型（Audio、Video、Game、Picture、Text）
    /// </summary>
    [VectorStoreData]
    public string MediaType { get; set; } = string.Empty;

    /// <summary>
    /// 分类名称
    /// </summary>
    [VectorStoreData]
    public string? CategoryName { get; set; }

    /// <summary>
    /// 分类ID
    /// </summary>
    [VectorStoreData]
    public int? CategoryId { get; set; }

    /// <summary>
    /// 社团/出版社名称
    /// </summary>
    [VectorStoreData]
    public string? CircleName { get; set; }

    /// <summary>
    /// 社团/出版社ID
    /// </summary>
    [VectorStoreData]
    public int? CircleId { get; set; }

    /// <summary>
    /// 简介
    /// </summary>
    [VectorStoreData]
    public string? Summary { get; set; }

    /// <summary>
    /// 发布日期（ISO 8601格式字符串）
    /// </summary>
    [VectorStoreData]
    public string? ReleaseDateString { get; set; }

    /// <summary>
    /// 评分
    /// </summary>
    [VectorStoreData]
    public double? Rating { get; set; }

    /// <summary>
    /// 标签列表（JSON序列化）
    /// </summary>
    [VectorStoreData]
    public string? TagsJson { get; set; }

    /// <summary>
    /// 别名列表（JSON序列化）
    /// </summary>
    [VectorStoreData]
    public string? AliasesJson { get; set; }

    public MediaVector()
    {
        RecordType = "Media";
    }
}