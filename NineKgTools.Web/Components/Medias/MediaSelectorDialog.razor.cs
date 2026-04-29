using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using Serilog;

namespace NineKgTools.Components.Medias;

public partial class MediaSelectorDialog : ComponentBase
{
    /// <summary>
    /// 初始已选择的媒体列表
    /// </summary>
    [Parameter] public List<MediaBase> InitialSelectedMedias { get; set; } = new();

    /// <summary>
    /// 要排除的媒体ID（通常是当前媒体本身）
    /// </summary>
    [Parameter] public int? ExcludeMediaId { get; set; }

    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject] private MediaService MediaService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isLoading;
    private string _searchTerm = string.Empty;
    private List<MediaBase> _searchResults = new();
    private List<MediaBase> _tempSelectedMedias = new();
    // O(1) 选中判定索引，与 _tempSelectedMedias 同步维护
    private readonly HashSet<int> _tempSelectedMediaIds = new();

    // 分类筛选
    private TopCategory? _selectedCategory;

    // 视图模式
    private bool _isGridView = true;

    /// <summary>
    /// 根据分类筛选后的结果
    /// </summary>
    private List<MediaBase> _filteredResults => _selectedCategory == null
        ? _searchResults
        : _searchResults.Where(m => m.Category?.TopCategory == _selectedCategory).ToList();

    /// <summary>
    /// 获取指定分类的搜索结果数量
    /// </summary>
    private int GetCategoryCount(TopCategory? category)
    {
        if (category == null)
            return _searchResults.Count;
        return _searchResults.Count(m => m.Category?.TopCategory == category);
    }

    protected override void OnInitialized()
    {
        // 复制初始选择的媒体到临时列表
        _tempSelectedMedias = new List<MediaBase>(InitialSelectedMedias);
        _tempSelectedMediaIds.Clear();
        foreach (var media in _tempSelectedMedias)
        {
            _tempSelectedMediaIds.Add(media.Id);
        }
    }

    private bool IsMediaSelected(MediaBase media) => _tempSelectedMediaIds.Contains(media.Id);

    private static string GetGridItemClass(bool isSelected) =>
        isSelected ? "media-selector-item selected" : "media-selector-item";

    private static string GetListItemClass(bool isSelected) =>
        isSelected ? "media-selector-list-item selected" : "media-selector-list-item";

    private static string GetMediaAriaLabel(MediaBase media, bool isSelected) =>
        isSelected ? $"{media.Title}，已选中" : media.Title;

    /// <summary>
    /// 搜索词变化事件处理
    /// </summary>
    private async Task HandleSearchTermChanged()
    {
        await SearchMedias();
    }

    /// <summary>
    /// 搜索媒体
    /// </summary>
    private async Task SearchMedias()
    {
        if (string.IsNullOrWhiteSpace(_searchTerm))
        {
            _searchResults.Clear();
            return;
        }

        try
        {
            _isLoading = true;
            StateHasChanged();

            // 使用简单的标题搜索
            _searchResults = await MediaService.SearchMediaByTitleAsync(
                _searchTerm,
                maxResults: 50, // 增加搜索结果数量以支持分类筛选
                excludeMediaId: ExcludeMediaId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "搜索媒体失败 SearchTerm={SearchTerm}", _searchTerm);
            Snackbar.Add("搜索媒体失败，请稍后重试。", Severity.Error);
            _searchResults.Clear();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 选择分类筛选
    /// </summary>
    private void SelectCategory(TopCategory? category)
    {
        _selectedCategory = category;
        StateHasChanged();
    }

    /// <summary>
    /// 切换视图模式
    /// </summary>
    private void OnViewModeToggled(bool toggled)
    {
        _isGridView = toggled;
        StateHasChanged();
    }

    /// <summary>
    /// 切换媒体选择状态。同步维护 List（保留插入顺序作为返回结果）+ HashSet（O(1) 查询）
    /// </summary>
    private void ToggleMedia(MediaBase media)
    {
        if (_tempSelectedMediaIds.Remove(media.Id))
        {
            _tempSelectedMedias.RemoveAll(m => m.Id == media.Id);
        }
        else
        {
            _tempSelectedMedias.Add(media);
            _tempSelectedMediaIds.Add(media.Id);
        }

        StateHasChanged();
    }

    // WAI-ARIA button 模式：Enter / Space 等同 click
    private void OnMediaKeyDown(KeyboardEventArgs e, MediaBase media)
    {
        if (e.Key == "Enter" || e.Key == " " || e.Key == "Spacebar")
        {
            ToggleMedia(media);
        }
    }

    // 0 选时点击确定实际是"清空关联"，用显式文案避免用户以为点了没事
    private string GetConfirmLabel()
    {
        var count = _tempSelectedMedias.Count;
        return count == 0 ? "清空并确定" : $"确定（{count} 项）";
    }

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(_tempSelectedMedias));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
