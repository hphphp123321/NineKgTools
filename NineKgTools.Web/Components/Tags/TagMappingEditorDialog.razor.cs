using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;

namespace NineKgTools.Components.Tags;

public partial class TagMappingEditorDialog : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// 编辑时传入的映射对象，null表示新建
    /// </summary>
    [Parameter]
    public TagMapping? Mapping { get; set; }

    /// <summary>
    /// 已存在的源名称列表（用于验证重复）
    /// </summary>
    [Parameter]
    public List<string> ExistingSourceNames { get; set; } = new();

    [Inject]
    private TagService TagService { get; set; } = null!;

    // 表单字段
    private string _sourceName = "";
    private Tag? _targetTag;
    private int _priority = 100;
    private bool _isActive = true;
    private string _description = "";

    private bool IsEditMode => Mapping != null;

    protected override void OnInitialized()
    {
        if (Mapping != null)
        {
            // 编辑模式：初始化表单字段
            _sourceName = Mapping.SourceName;
            _targetTag = Mapping.TargetTag;
            _priority = Mapping.Priority;
            _isActive = Mapping.IsActive;
            _description = Mapping.Description ?? "";
        }
        base.OnInitialized();
    }

    /// <summary>
    /// 选择目标标签
    /// </summary>
    private async Task SelectTargetTag()
    {
        var parameters = new DialogParameters<TagSelectorDialog>
        {
            { x => x.AllowMultiSelect, false },
            { x => x.InitialSelectedTags, _targetTag != null ? new List<Tag> { _targetTag } : new List<Tag>() }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true,
            CloseButton = true
        };

        var dialog = await DialogService.ShowAsync<TagSelectorDialog>("选择目标标签", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: List<Tag> tags } && tags.Count > 0)
        {
            _targetTag = tags.First();
            StateHasChanged();
        }
    }

    /// <summary>
    /// 取消
    /// </summary>
    private void Cancel()
    {
        MudDialog.Cancel();
    }

    /// <summary>
    /// 提交
    /// </summary>
    private void Submit()
    {
        // 验证
        if (string.IsNullOrWhiteSpace(_sourceName))
        {
            Snackbar.Add("源标签名称不能为空", Severity.Warning);
            return;
        }

        if (_targetTag == null)
        {
            Snackbar.Add("请选择目标标签", Severity.Warning);
            return;
        }

        // 检查重复（编辑模式下排除自己）
        var trimmedSourceName = _sourceName.Trim();
        var isDuplicate = ExistingSourceNames
            .Where(name => !IsEditMode || !name.Equals(Mapping?.SourceName, StringComparison.OrdinalIgnoreCase))
            .Any(name => name.Equals(trimmedSourceName, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate)
        {
            Snackbar.Add("源标签名称已存在", Severity.Warning);
            return;
        }

        // 构建结果
        var resultMapping = new TagMapping
        {
            Id = Mapping?.Id ?? 0,
            SourceName = trimmedSourceName,
            TargetTagId = _targetTag.Id,
            TargetTag = _targetTag,
            Priority = _priority,
            IsActive = _isActive,
            Description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
            CreatedAt = Mapping?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = IsEditMode ? DateTime.UtcNow : null,
            HitCount = Mapping?.HitCount ?? 0,
            LastHitAt = Mapping?.LastHitAt
        };

        MudDialog.Close(DialogResult.Ok(resultMapping));
    }
}
