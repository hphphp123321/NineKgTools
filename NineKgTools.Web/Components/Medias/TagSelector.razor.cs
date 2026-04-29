using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using Serilog;

namespace NineKgTools.Components.Medias;

public partial class TagSelector : ComponentBase
{
    // 客户端单次渲染上限，防止几千标签一次灌进 DOM
    private const int MaxRenderedTags = 200;

    [Parameter] public List<Tag> SelectedTags { get; set; } = new();
    [Parameter] public EventCallback<List<Tag>> SelectedTagsChanged { get; set; }
    [Parameter] public bool IsEditable { get; set; } = true;

    [Inject] private TagService TagService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private bool _isTagSelectorVisible;
    private bool _isLoading;
    private string _searchTerm = string.Empty;
    private List<Tag> _availableTags = new();
    private List<Tag> _tempSelectedTags = new();
    // O(1) 选中判定索引，与 _tempSelectedTags 同步维护
    private readonly HashSet<int> _tempSelectedTagIds = new();

    // 颜色数组，按顺序循环使用
    private readonly Color[] _tagColors = { 
        Color.Primary, Color.Secondary, Color.Info, 
        Color.Success, Color.Warning, Color.Tertiary };

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableTags();
    }

    // 加载可用标签。空搜索词会走服务层全表路径；客户端再做一次 Take 封顶，
    // 避免几千标签一次性渲染到 Expansion Panel 里
    private async Task LoadAvailableTags(string? searchTerm = null)
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            var tags = await TagService.GetAllTagsAsync(searchTerm);
            _availableTags = tags.Count > MaxRenderedTags
                ? tags.Take(MaxRenderedTags).ToList()
                : tags;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载标签失败 SearchTerm={SearchTerm}", searchTerm);
            Snackbar.Add("加载标签失败，请稍后重试。", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 搜索词变化事件处理
    /// </summary>
    private async Task HandleSearchTermChanged(string searchTerm)
    {
        _searchTerm = searchTerm;
        
        // 按照TagPage的逻辑：只有搜索词不为空时才进行搜索
        if (!string.IsNullOrEmpty(searchTerm))
        {
            await LoadAvailableTags(searchTerm);
        }
        else
        {
            // 搜索词为空时，加载所有标签
            await LoadAvailableTags(null);
        }
    }

    private bool IsTagSelected(Tag tag) => _tempSelectedTagIds.Contains(tag.Id);

    // 0 选时点击确定实际是"清空标签"，用显式文案避免静默破坏
    private string GetConfirmLabel()
    {
        var count = _tempSelectedTags.Count;
        return count == 0 ? "清空并确定" : $"确定（{count} 项）";
    }

    private async Task OpenTagSelector()
    {
        _tempSelectedTags = new List<Tag>(SelectedTags);
        _tempSelectedTagIds.Clear();
        foreach (var tag in _tempSelectedTags)
        {
            _tempSelectedTagIds.Add(tag.Id);
        }
        _searchTerm = string.Empty;
        _isTagSelectorVisible = true;
        // 加载所有可用标签（无过滤）
        await LoadAvailableTags(null);
    }

    private void CloseTagSelector()
    {
        _isTagSelectorVisible = false;
    }

    private void ToggleTag(Tag tag)
    {
        if (_tempSelectedTagIds.Remove(tag.Id))
        {
            _tempSelectedTags.RemoveAll(t => t.Id == tag.Id);
        }
        else
        {
            _tempSelectedTags.Add(tag);
            _tempSelectedTagIds.Add(tag.Id);
        }
        StateHasChanged();
    }

    /// <summary>
    /// 确认标签选择
    /// </summary>
    private async Task ConfirmTagSelection()
    {
        SelectedTags = new List<Tag>(_tempSelectedTags);
        await SelectedTagsChanged.InvokeAsync(SelectedTags);
        CloseTagSelector();
        Snackbar.Add($"已选择 {SelectedTags.Count} 个标签", Severity.Success);
    }

    /// <summary>
    /// 移除标签
    /// </summary>
    private async Task RemoveTag(Tag tag)
    {
        SelectedTags.Remove(tag);
        await SelectedTagsChanged.InvokeAsync(SelectedTags);
        Snackbar.Add($"已移除标签: {tag.Name}", Severity.Info);
    }

    /// <summary>
    /// 根据标签名称获取颜色
    /// </summary>
    private Color GetTagColor(string tagName)
    {
        if (string.IsNullOrEmpty(tagName))
            return Color.Default;

        // 使用标签名称的哈希值来确定颜色，确保同样的标签始终使用相同颜色
        int hash = Math.Abs(tagName.GetHashCode());
        return _tagColors[hash % _tagColors.Length];
    }

    private void NavigateToTagPage(int tagId)
    {
        Navigation.NavigateTo($"/tag/{tagId}");
    }

}