using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Medias;

public partial class DescriptionEditor : ComponentBase
{
    [Parameter] public string Description { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> DescriptionChanged { get; set; }

    [Parameter] public string DescriptionTranslated { get; set; } = string.Empty;
    [Parameter] public EventCallback<string> DescriptionTranslatedChanged { get; set; }

    [Parameter] public EventCallback OnTranslateRequested { get; set; }
    [Parameter] public bool IsTranslating { get; set; }

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private bool _isEditing;
    private bool _isTranslating;
    private string _originalDescription = string.Empty;

    public bool IsEditing => _isEditing;

    protected override void OnParametersSet()
    {
        _isTranslating = IsTranslating;
    }

    /// <summary>
    /// 切换编辑模式（公开方法，供外部调用）
    /// </summary>
    public void ToggleEditMode()
    {
        if (_isEditing)
        {
            // 从编辑模式切换到预览模式，保存更改
            SaveDescription();
        }
        else
        {
            // 从预览模式切换到编辑模式
            StartEditing();
        }
    }

    /// <summary>
    /// 开始编辑
    /// </summary>
    private void StartEditing()
    {
        _isEditing = true;
        _originalDescription = Description;
    }

    /// <summary>
    /// 保存描述
    /// </summary>
    private async Task SaveDescription()
    {
        _isEditing = false;
        await DescriptionChanged.InvokeAsync(Description);
        Snackbar.Add("描述已保存", Severity.Success);
    }

    /// <summary>
    /// 取消编辑
    /// </summary>
    private void CancelEditing()
    {
        _isEditing = false;
        Description = _originalDescription; // 恢复原始内容
    }

    /// <summary>
    /// 描述变更事件处理
    /// </summary>
    private async Task OnDescriptionChanged()
    {
        await DescriptionChanged.InvokeAsync(Description);
    }

    /// <summary>
    /// 翻译描述变更事件处理
    /// </summary>
    private async Task OnDescriptionTranslatedChanged()
    {
        await DescriptionTranslatedChanged.InvokeAsync(DescriptionTranslated);
    }

    /// <summary>
    /// 翻译请求事件处理
    /// </summary>
    private async Task OnTranslateClicked()
    {
        await OnTranslateRequested.InvokeAsync();
    }

    /// <summary>
    /// 退出编辑模式（供外部调用，丢弃未保存的描述修改）
    /// </summary>
    public void ExitEditMode()
    {
        if (_isEditing)
        {
            CancelEditing();
        }
    }
}