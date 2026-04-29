using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tasks;

namespace NineKgTools.Components.Medias;

public partial class IdentificationLoadingDialog : ComponentBase, IDisposable
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// 进度百分比 (0-100)
    /// </summary>
    [Parameter]
    public double Progress { get; set; } = 0;

    /// <summary>
    /// 当前消息
    /// </summary>
    [Parameter]
    public string Message { get; set; } = "正在准备...";

    /// <summary>
    /// 当前处理项
    /// </summary>
    [Parameter]
    public string? CurrentItem { get; set; }

    /// <summary>
    /// 取消回调
    /// </summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    /// <summary>
    /// 处理统一的进度条目
    /// </summary>
    /// <param name="entry">进度条目</param>
    public void HandleProgress(TaskLogEntry entry)
    {
        Progress = entry.Progress ?? Progress;
        if (!string.IsNullOrEmpty(entry.Message))
            Message = entry.Message;
        if (!string.IsNullOrEmpty(entry.CurrentItem))
            CurrentItem = entry.CurrentItem;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 更新进度信息
    /// </summary>
    /// <param name="progress">进度百分比</param>
    /// <param name="message">消息（null则保持原值）</param>
    /// <param name="currentItem">当前处理项（null则保持原值）</param>
    public void UpdateProgress(double progress, string? message = null, string? currentItem = null)
    {
        Progress = progress;
        if (message != null)
            Message = message;
        if (currentItem != null)
            CurrentItem = currentItem;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 处理取消操作
    /// </summary>
    private async Task HandleCancel()
    {
        if (OnCancel.HasDelegate)
        {
            await OnCancel.InvokeAsync();
        }
        MudDialog.Close(DialogResult.Cancel());
    }

    /// <summary>
    /// 关闭对话框
    /// </summary>
    /// <param name="success">是否成功</param>
    public void Close(bool success = true)
    {
        MudDialog.Close(success ? DialogResult.Ok(true) : DialogResult.Cancel());
    }

    public void Dispose()
    {
        // 清理资源（如果需要）
    }
}
