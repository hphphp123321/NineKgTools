namespace NineKgTools.Core.Models.Vectors;

/// <summary>
/// 向量搜索结果
/// </summary>
/// <typeparam name="T">向量记录类型</typeparam>
public record VectorSearchResult<T>(T? Record, double Score) where T : BaseVector;