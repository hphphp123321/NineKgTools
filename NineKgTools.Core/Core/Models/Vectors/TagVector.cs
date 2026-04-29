using Microsoft.Extensions.VectorData;

namespace NineKgTools.Core.Models.Vectors;

/// <summary>
/// 标签向量记录模型
/// </summary>
public class TagVector : BaseVector
{
    /// <summary>
    /// 标签ID
    /// </summary>
    [VectorStoreData]
    public int TagId { get; set; }

    /// <summary>
    /// 标签名称（使用基类的Text存储）
    /// </summary>
    public string TagName
    {
        get => Text;
        set => Text = value;
    }

    /// <summary>
    /// 标签描述
    /// </summary>
    [VectorStoreData]
    public string? Description { get; set; }

    /// <summary>
    /// 顶级标签名称
    /// </summary>
    [VectorStoreData]
    public string? TopTagName { get; set; }

    /// <summary>
    /// 顶级标签ID
    /// </summary>
    [VectorStoreData]
    public int? TopTagId { get; set; }

    public TagVector()
    {
        RecordType = "Tag";
    }
}