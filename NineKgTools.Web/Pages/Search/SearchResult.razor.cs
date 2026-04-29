using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.WebUtilities;
using MudBlazor;
using NineKgTools.Components.Search;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Search;
using Serilog;

namespace NineKgTools.Pages.Search;

public partial class SearchResult : ComponentBase, IDisposable
{
    [Inject] protected GlobalSearchService GlobalSearchService { get; set; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    
    // 搜索状态
    protected GlobalSearchOptions _searchOptions = new()
    {
        EntityTypes = SearchEntityTypes.All,
        EnableVectorSearch = false,
        MaxResultsPerType = 50,
        MinRelevanceScore = 0.3
    };
    
    protected GlobalSearchResult? _searchResult;
    protected string _currentQuery = string.Empty;
    protected bool _isLoading = false;
    protected string _errorMessage = string.Empty;
    protected bool _disposed = false;
    
    // 取消令牌
    private CancellationTokenSource? _searchCancellationTokenSource;

    protected override async Task OnInitializedAsync()
    {
        // 从URL参数解析搜索选项
        ParseUrlParameters();
        
        // 如果有查询参数，执行搜索
        if (!string.IsNullOrWhiteSpace(_searchOptions.Query))
        {
            _currentQuery = _searchOptions.Query;
            await ExecuteSearchInternal();
        }
    }

    /// <summary>
    /// 从URL参数解析搜索选项
    /// </summary>
    protected void ParseUrlParameters()
    {
        try
        {
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var queryParams = QueryHelpers.ParseQuery(uri.Query);
            
            // 解析查询关键词
            if (queryParams.TryGetValue("q", out var query))
            {
                _searchOptions.Query = query.FirstOrDefault() ?? string.Empty;
            }
            
            // 解析实体类型
            if (queryParams.TryGetValue("types", out var types))
            {
                if (Enum.TryParse<SearchEntityTypes>(types.FirstOrDefault(), out var entityTypes))
                {
                    _searchOptions.EntityTypes = entityTypes;
                }
            }
            
            // 解析向量搜索开关
            if (queryParams.TryGetValue("vector", out var vectorSearch))
            {
                if (bool.TryParse(vectorSearch.FirstOrDefault(), out var enableVector))
                {
                    _searchOptions.EnableVectorSearch = enableVector;
                }
            }
            
            // 解析最大结果数
            if (queryParams.TryGetValue("limit", out var limit))
            {
                if (int.TryParse(limit.FirstOrDefault(), out var maxResults) && maxResults > 0)
                {
                    _searchOptions.MaxResultsPerType = Math.Min(maxResults, 100); // 限制最大值
                }
            }
            
            // 解析最小相关性分数
            if (queryParams.TryGetValue("score", out var score))
            {
                if (double.TryParse(score.FirstOrDefault(), out var minScore) && minScore >= 0 && minScore <= 1)
                {
                    _searchOptions.MinRelevanceScore = minScore;
                }
            }
            
            // 解析复杂筛选器（JSON格式）
            if (queryParams.TryGetValue("filters", out var filtersJson))
            {
                try
                {
                    var decodedFilters = HttpUtility.UrlDecode(filtersJson.FirstOrDefault());
                    if (!string.IsNullOrEmpty(decodedFilters))
                    {
                        var filters = JsonSerializer.Deserialize<SearchFilters>(decodedFilters);
                        ApplyFilters(filters);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "解析搜索筛选器失败");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "解析URL参数失败");
        }
    }

    /// <summary>
    /// 应用筛选器到搜索选项
    /// </summary>
    protected void ApplyFilters(SearchFilters? filters)
    {
        if (filters == null) return;
        
        _searchOptions.CategoryFilter = filters.CategoryFilter;
        _searchOptions.TagFilter = filters.TagFilter;
        _searchOptions.RatingFilter = filters.RatingFilter;
    }

    /// <summary>
    /// 更新URL参数
    /// </summary>
    protected void UpdateUrlParameters()
    {
        try
        {
            var queryParams = new Dictionary<string, string?>();
            
            // 基本参数
            if (!string.IsNullOrWhiteSpace(_searchOptions.Query))
                queryParams["q"] = _searchOptions.Query;
            
            if (_searchOptions.EntityTypes != SearchEntityTypes.All)
                queryParams["types"] = _searchOptions.EntityTypes.ToString();
            
            if (_searchOptions.EnableVectorSearch)
                queryParams["vector"] = "true";
            
            if (_searchOptions.MaxResultsPerType != 50)
                queryParams["limit"] = _searchOptions.MaxResultsPerType.ToString();
            
            if (_searchOptions.MinRelevanceScore != 0.3)
                queryParams["score"] = _searchOptions.MinRelevanceScore.ToString("F2");
            
            // 复杂筛选器
            var filters = new SearchFilters
            {
                CategoryFilter = _searchOptions.CategoryFilter,
                TagFilter = _searchOptions.TagFilter,
                RatingFilter = _searchOptions.RatingFilter
            };
            
            if (HasActiveFilters(filters))
            {
                var filtersJson = JsonSerializer.Serialize(filters);
                queryParams["filters"] = HttpUtility.UrlEncode(filtersJson);
            }
            
            // 构建新URL
            var newUri = QueryHelpers.AddQueryString("/search", queryParams);
            NavigationManager.NavigateTo(newUri, replace: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新URL参数失败");
        }
    }

    /// <summary>
    /// 检查是否有活动的筛选器
    /// </summary>
    protected bool HasActiveFilters(SearchFilters filters)
    {
        return filters.CategoryFilter?.CategoryIds.Any() == true ||
               filters.TagFilter?.TagIds.Any() == true ||
               filters.RatingFilter != null;
    }

    /// <summary>
    /// 执行搜索
    /// </summary>
    protected async Task ExecuteSearch()
    {
        if (string.IsNullOrWhiteSpace(_currentQuery))
        {
            Snackbar.Add("请输入搜索关键词", Severity.Warning);
            return;
        }
        
        _searchOptions.Query = _currentQuery.Trim();
        UpdateUrlParameters();
        await ExecuteSearchInternal();
    }

    /// <summary>
    /// 内部搜索执行方法
    /// </summary>
    protected async Task ExecuteSearchInternal()
    {
        if (_disposed) return;
        
        try
        {
            // 取消之前的搜索
            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource = new CancellationTokenSource();
            
            _isLoading = true;
            _errorMessage = string.Empty;
            _searchResult = null;
            StateHasChanged();
            
            // 设置取消令牌
            _searchOptions.CancellationToken = _searchCancellationTokenSource.Token;
            
            // 执行搜索
            _searchResult = await GlobalSearchService.SearchAsync(_searchOptions);
            
            if (_searchResult.TotalCount == 0)
            {
                Snackbar.Add($"未找到与 \"{_searchOptions.Query}\" 相关的内容", Severity.Info);
            }
            else
            {
                Snackbar.Add($"找到 {_searchResult.TotalCount} 个结果", Severity.Success);
            }
        }
        catch (OperationCanceledException)
        {
            // 搜索被取消，不显示错误
            Log.Information("搜索被取消: {Query}", _searchOptions.Query);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "搜索失败: {Query}", _searchOptions.Query);
            _errorMessage = $"搜索失败: {ex.Message}";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            if (!_disposed)
            {
                _isLoading = false;
                StateHasChanged();
            }
        }
    }

    /// <summary>
    /// 刷新搜索
    /// </summary>
    protected async Task RefreshSearch()
    {
        if (!string.IsNullOrWhiteSpace(_searchOptions.Query))
        {
            await ExecuteSearchInternal();
        }
    }

    /// <summary>
    /// 处理搜索框键盘事件
    /// </summary>
    protected async Task OnSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await ExecuteSearch();
        }
    }

    /// <summary>
    /// 切换实体类型
    /// </summary>
    protected void ToggleEntityType(SearchEntityTypes entityType)
    {
        if (_searchOptions.EntityTypes.HasFlag(entityType))
        {
            _searchOptions.EntityTypes &= ~entityType;
        }
        else
        {
            _searchOptions.EntityTypes |= entityType;
        }
        
        // 至少保留一个类型
        if (_searchOptions.EntityTypes == SearchEntityTypes.None)
        {
            _searchOptions.EntityTypes = entityType;
        }
        
        UpdateUrlParameters();
        StateHasChanged();
    }

    /// <summary>
    /// 启用向量搜索
    /// </summary>
    protected async Task EnableVectorSearch()
    {
        _searchOptions.EnableVectorSearch = true;
        UpdateUrlParameters();
        
        if (!string.IsNullOrWhiteSpace(_searchOptions.Query))
        {
            await ExecuteSearchInternal();
        }
    }

    /// <summary>
    /// 打开筛选器对话框
    /// </summary>
    protected async Task OpenFilterDialog()
    {
        try
        {
            var parameters = new DialogParameters<SearchFilterDialog>
            {
                { x => x.InitialOptions, _searchOptions }
            };
            
            var options = new DialogOptions
            {
                CloseButton = true,
                MaxWidth = MaxWidth.Medium,
                FullWidth = true,
                CloseOnEscapeKey = true
            };
            
            var dialog = await DialogService.ShowAsync<SearchFilterDialog>("搜索筛选", parameters, options);
            var result = await dialog.Result;
            
            if (!result.Canceled && result.Data is GlobalSearchOptions newOptions && !_disposed)
            {
                _searchOptions = newOptions;
                _searchOptions.Query = _currentQuery; // 保持当前查询
                
                UpdateUrlParameters();
                
                // 如果有查询，重新搜索
                if (!string.IsNullOrWhiteSpace(_searchOptions.Query))
                {
                    await ExecuteSearchInternal();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开搜索筛选器失败");
            Snackbar.Add("打开筛选器失败", Severity.Error);
        }
    }

    /// <summary>
    /// 获取匹配类型文本
    /// </summary>
    protected string GetMatchTypeText(SearchMatchType matchType)
    {
        return matchType switch
        {
            SearchMatchType.Exact => "精确",
            SearchMatchType.Fuzzy => "模糊",
            SearchMatchType.Contains => "包含",
            SearchMatchType.Vector => "语义",
            SearchMatchType.Alias => "别名",
            SearchMatchType.Description => "描述",
            _ => "未知"
        };
    }

    /// <summary>
    /// 导航到标签页面
    /// </summary>
    protected void NavigateToTag(int tagId)
    {
        NavigationManager.NavigateTo($"/tag/{tagId}");
    }

    /// <summary>
    /// 导航到社团页面
    /// </summary>
    protected void NavigateToCircle(int circleId)
    {
        NavigationManager.NavigateTo($"/circle/{circleId}");
    }

    /// <summary>
    /// 导航到创作者页面
    /// </summary>
    protected void NavigateToCreator(int creatorId)
    {
        NavigationManager.NavigateTo($"/creator/{creatorId}");
    }

    public void Dispose()
    {
        _disposed = true;
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 搜索筛选器数据传输对象
/// </summary>
public class SearchFilters
{
    public CategoryFilter? CategoryFilter { get; set; }
    public TagFilter? TagFilter { get; set; }
    public RatingFilter? RatingFilter { get; set; }
}
