using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Search;

namespace NineKgTools.Core.Services.Search.Strategies;

/// <summary>
/// 搜索策略基础接口
/// </summary>
public interface ISearchStrategy<T>
{
    /// <summary>
    /// 执行搜索
    /// </summary>
    /// <param name="query">搜索关键词</param>
    /// <param name="options">搜索选项</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>搜索结果列表</returns>
    Task<List<SearchResultItem<T>>> SearchAsync(
        string query, 
        GlobalSearchOptions options,
        CancellationToken cancellationToken);
}