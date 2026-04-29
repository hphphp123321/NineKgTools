using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;

namespace NineKgTools.Components.Tags;

public partial class TagSelectorDialog : ComponentBase
{
    /// <summary>
    /// MudDialog实例（通过DialogService.ShowAsync使用时可用）
    /// </summary>
    [CascadingParameter]
    private IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public bool AllowMultiSelect { get; set; } = true;
    [Parameter] public List<Tag> InitialSelectedTags { get; set; } = new();
    [Parameter] public EventCallback<List<Tag>> OnTagsSelected { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    [Inject] protected TagService TagService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;

    protected bool _isLoading = false;
    protected string _searchTerm = string.Empty;
    protected List<Tag> _filteredTags = new();
    protected List<Tag> _tempSelectedTags = new();

    protected override async Task OnInitializedAsync()
    {
        _tempSelectedTags = new List<Tag>(InitialSelectedTags);
        await LoadTags();
    }

    /// <summary>
    /// 加载标签数据
    /// </summary>
    protected async Task LoadTags()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            // 根据搜索条件获取标签
            if (string.IsNullOrWhiteSpace(_searchTerm))
            {
                _filteredTags = await TagService.GetAllTagsAsync();
            }
            else
            {
                _filteredTags = await TagService.GetAllTagsAsync(_searchTerm);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"加载标签失败: {ex.Message}", Severity.Error);
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
    protected async Task HandleSearchTermChanged()
    {
        await LoadTags();
    }

    /// <summary>
    /// 切换标签选择状态
    /// </summary>
    protected void ToggleTag(Tag tag)
    {
        var existingTag = _tempSelectedTags.FirstOrDefault(t => t.Id == tag.Id);
        
        if (existingTag != null)
        {
            _tempSelectedTags.Remove(existingTag);
        }
        else
        {
            if (!AllowMultiSelect)
            {
                // 单选模式：清除之前的选择
                _tempSelectedTags.Clear();
            }
            _tempSelectedTags.Add(tag);
        }
        
        StateHasChanged();
    }

    /// <summary>
    /// 确认选择
    /// </summary>
    protected async Task Confirm()
    {
        // 如果是通过DialogService.ShowAsync使用，通过MudDialog返回结果
        if (MudDialog != null)
        {
            MudDialog.Close(DialogResult.Ok(_tempSelectedTags));
        }
        else
        {
            // 否则使用EventCallback
            await OnTagsSelected.InvokeAsync(_tempSelectedTags);
        }
    }

    /// <summary>
    /// 取消选择
    /// </summary>
    protected async Task Cancel()
    {
        // 如果是通过DialogService.ShowAsync使用，通过MudDialog取消
        if (MudDialog != null)
        {
            MudDialog.Cancel();
        }
        else
        {
            // 否则使用EventCallback
            await OnCancel.InvokeAsync();
        }
    }
}