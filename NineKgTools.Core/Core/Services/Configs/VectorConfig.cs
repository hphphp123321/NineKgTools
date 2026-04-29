using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

/// <summary>
/// 向量配置（统一管理所有向量相关设置）
/// </summary>
public class VectorConfig
{
    /// <summary>
    /// 向量功能总开关
    /// </summary>
    [YamlMember(Alias = "enable", Description = "是否启用向量功能")]
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 媒体向量配置
    /// </summary>
    [YamlMember(Alias = "media", Description = "媒体向量配置")]
    public MediaVectorConfig Media { get; set; } = new();

    /// <summary>
    /// 标签向量配置
    /// </summary>
    [YamlMember(Alias = "tag", Description = "标签向量配置")]
    public TagVectorConfig Tag { get; set; } = new();

    /// <summary>
    /// 搜索配置
    /// </summary>
    [YamlMember(Alias = "search", Description = "向量搜索配置")]
    public VectorSearchSettings Search { get; set; } = new();

    /// <summary>
    /// 向量数据库配置
    /// </summary>
    [YamlMember(Alias = "db", Description = "向量数据库配置")]
    public VectorDbConfig Db { get; set; } = new();

    public VectorConfig Copy()
    {
        return new VectorConfig
        {
            Enable = Enable,
            Media = Media.Copy(),
            Tag = Tag.Copy(),
            Search = Search.Copy(),
            Db = Db.Copy()
        };
    }
}

/// <summary>
/// 媒体向量配置
/// </summary>
public class MediaVectorConfig
{
    /// <summary>
    /// 是否启用媒体向量（包括索引和搜索）
    /// </summary>
    [YamlMember(Alias = "enable", Description = "是否启用媒体向量")]
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 搜索最小相似度阈值
    /// </summary>
    [YamlMember(Alias = "min_similarity", Description = "搜索最小相似度（0-1）")]
    public double MinSimilarity { get; set; } = 0.7;

    public MediaVectorConfig Copy()
    {
        return new MediaVectorConfig
        {
            Enable = Enable,
            MinSimilarity = MinSimilarity
        };
    }
}

/// <summary>
/// 标签向量配置
/// </summary>
public class TagVectorConfig
{
    /// <summary>
    /// 是否启用标签向量（包括匹配和搜索）
    /// </summary>
    [YamlMember(Alias = "enable", Description = "是否启用标签向量")]
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 标签匹配相似度阈值
    /// </summary>
    [YamlMember(Alias = "similarity_threshold", Description = "匹配相似度阈值（0-1）")]
    public double SimilarityThreshold { get; set; } = 0.05;

    /// <summary>
    /// 向量搜索返回的最大结果数
    /// </summary>
    [YamlMember(Alias = "search_top_k", Description = "搜索返回最大结果数")]
    public int SearchTopK { get; set; } = 3;

    public TagVectorConfig Copy()
    {
        return new TagVectorConfig
        {
            Enable = Enable,
            SimilarityThreshold = SimilarityThreshold,
            SearchTopK = SearchTopK
        };
    }
}

/// <summary>
/// 向量搜索设置
/// </summary>
public class VectorSearchSettings
{
    /// <summary>
    /// 向量搜索在综合结果中的权重
    /// </summary>
    [YamlMember(Alias = "weight", Description = "向量搜索权重（0-1）")]
    public double Weight { get; set; } = 0.6;

    public VectorSearchSettings Copy()
    {
        return new VectorSearchSettings
        {
            Weight = Weight
        };
    }
}

/// <summary>
/// 向量数据库配置
/// </summary>
public class VectorDbConfig
{
    /// <summary>
    /// 向量数据库提供者
    /// </summary>
    [YamlMember(Alias = "provider", Description = "向量数据库提供者")]
    public string Provider { get; set; } = "sqlite";

    /// <summary>
    /// 数据库连接字符串
    /// </summary>
    [YamlMember(Alias = "connection_string", Description = "数据库连接字符串")]
    public string ConnectionString { get; set; } = "Data Source=Database/vectors.db";

    /// <summary>
    /// 向量维度
    /// </summary>
    [YamlMember(Alias = "dimension", Description = "向量维度")]
    public int Dimension { get; set; } = 1536;

    /// <summary>
    /// 批处理大小
    /// </summary>
    [YamlMember(Alias = "batch_size", Description = "批处理大小")]
    public int BatchSize { get; set; } = 100;

    public VectorDbConfig Copy()
    {
        return new VectorDbConfig
        {
            Provider = Provider,
            ConnectionString = ConnectionString,
            Dimension = Dimension,
            BatchSize = BatchSize
        };
    }
}
