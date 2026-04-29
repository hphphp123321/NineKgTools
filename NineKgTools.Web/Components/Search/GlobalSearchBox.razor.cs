using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using NineKgTools.Core.Models.Search;
using NineKgTools.Core.Services.Search;
using Serilog;

namespace NineKgTools.Components.Search;

public partial class GlobalSearchBox : ComponentBase, IDisposable
{
    [Inject] protected GlobalSearchService GlobalSearchService { get; set; } = null!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected NavigationManager NavigationManager { get; set; } = null!;
    
    // 搜索输入相关状态
    protected string _searchQuery = string.Empty;
    protected string _userInputQuery = string.Empty; // 保存用户实际输入的查询，不被占位符覆盖
    protected string _lastSearchQuery = string.Empty; // 跟踪最后的搜索查询，用于NoItemsTemplate状态判断
    protected bool _isSearching = false;
    protected bool _disposed = false;

    // 中文输入法（IME）组合状态：为 true 时用户还在拼音中间态，
    // MudAutocomplete 的 SearchFunc 每次输入都会被调用，这里用它过滤掉
    // "ren"→"ren'q"→"ren'qi" 这类无意义的中间态搜索。compositionend 时
    // 输入值已经是最终汉字（如"人妻"），MudAutocomplete 会再触发一次 SearchFunc。
    protected bool _isImeComposing = false;
    
    // 搜索结果相关状态
    protected GlobalSearchResult? _currentSearchResult;
    protected GlobalSearchOptions _searchOptions = new()
    {
        EntityTypes = SearchEntityTypes.All,
        EnableVectorSearch = false,
        MaxResultsPerType = 20,
        MinRelevanceScore = 0.3
    };
    

    
    /// <summary>
    /// 搜索建议函数 - 执行搜索并返回结果用于ItemTemplate显示
    /// </summary>
    protected async Task<IEnumerable<string?>?> SearchSuggestions(string value, CancellationToken cancellationToken)
    {
        if (_disposed) return null;

        // 保存用户实际输入的查询，避免被占位符覆盖
        if (!string.IsNullOrWhiteSpace(value) && value != "search-results" && value != "search-error")
        {
            _userInputQuery = value;
        }

        // 拼音中间态（如 "ren"→"ren'q"→"ren'qi"）不要发搜索，等 compositionend
        // 把汉字敲定后 MudAutocomplete 会自动再调一次 SearchFunc。
        if (_isImeComposing)
        {
            return Enumerable.Empty<string?>();
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            _currentSearchResult = null;
            _lastSearchQuery = string.Empty; // 重置最后搜索查询
            StateHasChanged();
            // 返回空集合以触发NoItemsTemplate显示初始状态，而不是返回null
            return Enumerable.Empty<string?>();
        }

        try
        {
            _isSearching = true;
            StateHasChanged();

            // 执行搜索
            _searchOptions.Query = value;
            _lastSearchQuery = value; // 记录最后的搜索查询
            _currentSearchResult = await GlobalSearchService.SearchAsync(_searchOptions);

            // 返回一个项目以触发ItemTemplate显示搜索结果
            if (_currentSearchResult?.TotalCount > 0)
            {
                return new[] { value }; // 返回占位符以显示ItemTemplate
            }

            // 重要：当无结果时返回空集合以触发 NoItemsTemplate，而不是返回 null
            // 返回 null 会被 MudAutocomplete 视为不更新列表，导致 NoItemsTemplate 不显示
            return Enumerable.Empty<string?>();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻断用户体验
            Log.Error(ex, "搜索建议获取失败");
            _currentSearchResult = new GlobalSearchResult
            {
                Query = value,
                ErrorMessage = $"搜索失败: {ex.Message}"
            };
            return ["search-error"]; // 返回错误占位符
        }
        finally
        {
            _isSearching = false;
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// IME 开始组合输入（用户开始敲拼音/日文罗马字等）。此阶段 MudAutocomplete 每次
    /// 按键都会以中间态文本触发 SearchFunc，SearchSuggestions 会检查这个标志直接返回空。
    /// </summary>
    protected void OnCompositionStart() => _isImeComposing = true;

    /// <summary>
    /// IME 组合结束。此时输入框 value 是最终敲定的汉字，MudAutocomplete 会再触发一次
    /// SearchFunc 用这个最终值去搜索——所以本方法里无需手动触发。
    /// </summary>
    protected void OnCompositionEnd() => _isImeComposing = false;

    /// <summary>
    /// 处理文本变化事件
    /// </summary>
    protected void OnTextChanged(string value)
    {
        if (_disposed) return;

        // 只有当文本不是占位符时才更新用户输入查询
        if (!string.IsNullOrEmpty(value) && value != "search-results" && value != "search-error")
        {
            _userInputQuery = value;
        }
        else if (string.IsNullOrEmpty(value))
        {
            _userInputQuery = string.Empty;
        }
    }

    /// <summary>
    /// 处理键盘事件
    /// </summary>
    protected void OnKeyDown(KeyboardEventArgs args)
    {
        if (_disposed) return;

        if (args.Key == "Enter")
        {
            // 使用用户实际输入的查询，而不是可能被占位符覆盖的_searchQuery
            var queryToUse = !string.IsNullOrWhiteSpace(_userInputQuery) ? _userInputQuery : _searchQuery;
            
            // 清空_searchQuery
            _searchQuery = string.Empty;
            StateHasChanged();
            
            if (!string.IsNullOrWhiteSpace(queryToUse))
            {
                NavigateToSearchResults(queryToUse.Trim());
            }
        }
        else if (args.Key == "Escape")
        {
            _searchQuery = string.Empty;
            _userInputQuery = string.Empty;
            StateHasChanged();
        }
    }
    
    /// <summary>
    /// 执行搜索
    /// </summary>
    protected void OnSearchClicked()
    {
        if (_disposed) return;

        // 使用用户实际输入的查询，而不是可能被占位符覆盖的_searchQuery
        var queryToUse = !string.IsNullOrWhiteSpace(_userInputQuery) ? _userInputQuery : _searchQuery;
        if (!string.IsNullOrWhiteSpace(queryToUse))
        {
            NavigateToSearchResults(queryToUse.Trim());
        }
    }
    
    /// <summary>
    /// 打开筛选器对话框
    /// </summary>
    protected async Task OpenFilterDialog()
    {
        await HandleOpenFilter();
    }
    
    /// <summary>
    /// 处理打开筛选器事件
    /// </summary>
    protected async Task HandleOpenFilter()
    {
        if (_disposed) return;
        
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
                
                // 如果有当前搜索查询，重新搜索应用新筛选
                if (!string.IsNullOrWhiteSpace(newOptions.Query))
                {
                    // 重新执行搜索
                    StateHasChanged();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开搜索筛选器失败");
        }
    }
    
    /// <summary>
    /// 处理查看全部结果事件
    /// </summary>
    protected void HandleViewAllResults()
    {
        // 使用用户实际输入的查询或搜索选项中的查询
        var queryToUse = !string.IsNullOrWhiteSpace(_userInputQuery) ? _userInputQuery : _searchOptions.Query;
        if (!string.IsNullOrWhiteSpace(queryToUse))
        {
            NavigateToSearchResults(queryToUse);
        }
    }
    
    /// <summary>
    /// 处理项目点击事件
    /// </summary>
    protected void HandleItemClick(string itemInfo)
    {
        // 解析项目信息并导航到对应页面
        if (!string.IsNullOrEmpty(itemInfo))
        {
            var parts = itemInfo.Split(':');
            if (parts.Length == 2)
            {
                var itemType = parts[0];
                var itemId = parts[1];
                
                // 根据类型导航到不同页面
                switch (itemType.ToLower())
                {
                    case "media":
                        // NavigationManager.NavigateTo($"/media/{itemId}");
                        break;
                    case "tag":
                        // NavigationManager.NavigateTo($"/tags/{itemId}");
                        break;
                    case "circle":
                        // NavigationManager.NavigateTo($"/circles/{itemId}");
                        break;
                    case "creator":
                        // NavigationManager.NavigateTo($"/creators/{itemId}");
                        break;
                }
            }
        }
        
        StateHasChanged();
    }
    
    /// <summary>
    /// 清除搜索
    /// </summary>
    public void ClearSearch()
    {
        _searchQuery = string.Empty;
        _userInputQuery = string.Empty;
        StateHasChanged();
    }

    /// <summary>
    /// 设置搜索查询
    /// </summary>
    public void SetSearchQuery(string query)
    {
        _searchQuery = query;
        _userInputQuery = query;
        StateHasChanged();
    }
    
    /// <summary>
    /// 导航到搜索结果页面
    /// </summary>
    protected void NavigateToSearchResults(string query)
    {
        try
        {
            var queryParams = new Dictionary<string, string?>
            {
                ["q"] = query
            };

            // 添加搜索选项参数
            if (_searchOptions.EntityTypes != SearchEntityTypes.All)
                queryParams["types"] = _searchOptions.EntityTypes.ToString();

            if (_searchOptions.EnableVectorSearch)
                queryParams["vector"] = "true";

            if (_searchOptions.MaxResultsPerType != 20)
                queryParams["limit"] = _searchOptions.MaxResultsPerType.ToString();

            if (_searchOptions.MinRelevanceScore != 0.3)
                queryParams["score"] = _searchOptions.MinRelevanceScore.ToString("F2");

            // 构建URL
            var searchUrl = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString("/search", queryParams);
            NavigationManager.NavigateTo(searchUrl);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导航到搜索结果页面失败");
        }
    }

    /// <summary>
    /// 聚焦到搜索框
    /// </summary>
    public async Task FocusAsync()
    {
        try
        {
            await JSRuntimeExtensions.InvokeVoidAsync(JSRuntime, "focusElement", ".search-autocomplete input");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "聚焦搜索框失败");
        }
    }
    
    public void Dispose()
    {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}