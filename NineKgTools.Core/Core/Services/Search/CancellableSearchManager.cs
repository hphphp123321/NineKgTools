using System;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Configs;

namespace NineKgTools.Core.Services.Search;

/// <summary>
/// 可取消搜索管理器
/// </summary>
public class CancellableSearchManager : IDisposable
{
    private readonly SearchConfig _searchConfig;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private CancellationTokenSource? _currentSearchCts;
    private readonly object _lock = new();
    
    public CancellableSearchManager(SearchConfig? searchConfig = null)
    {
        _searchConfig = searchConfig ?? new SearchConfig();
        _concurrencyLimiter = new SemaphoreSlim(_searchConfig.MaxConcurrentSearches, _searchConfig.MaxConcurrentSearches);
    }
    
    /// <summary>
    /// 执行可取消的搜索
    /// </summary>
    public async Task<GlobalSearchResult> ExecuteSearchAsync(
        GlobalSearchOptions options,
        Func<CancellationToken, Task<GlobalSearchResult>> searchFunc)
    {
        // 等待并发许可
        await _concurrencyLimiter.WaitAsync(options.CancellationToken);
        
        try
        {
            CancellationTokenSource newCts;
            
            lock (_lock)
            {
                // 取消之前的搜索
                _currentSearchCts?.Cancel();
                _currentSearchCts?.Dispose();
                
                // 创建新的取消令牌，并添加超时
                newCts = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken);
                if (_searchConfig.SearchTimeoutSeconds > 0)
                {
                    newCts.CancelAfter(TimeSpan.FromSeconds(_searchConfig.SearchTimeoutSeconds));
                }
                _currentSearchCts = newCts;
            }
            
            try
            {
                return await searchFunc(newCts.Token);
            }
            catch (OperationCanceledException)
            {
                return new GlobalSearchResult 
                { 
                    Query = options.Query,
                    WasCancelled = true 
                };
            }
            finally
            {
                lock (_lock)
                {
                    if (_currentSearchCts == newCts)
                    {
                        _currentSearchCts = null;
                    }
                    newCts.Dispose();
                }
            }
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
    
    /// <summary>
    /// 取消当前搜索
    /// </summary>
    public void CancelCurrentSearch()
    {
        lock (_lock)
        {
            _currentSearchCts?.Cancel();
        }
    }
    
    /// <summary>
    /// 是否有正在进行的搜索
    /// </summary>
    public bool HasActiveSearch
    {
        get
        {
            lock (_lock)
            {
                return _currentSearchCts != null && !_currentSearchCts.Token.IsCancellationRequested;
            }
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _currentSearchCts?.Cancel();
            _currentSearchCts?.Dispose();
            _currentSearchCts = null;
        }
        
        _concurrencyLimiter.Dispose();
    }
}