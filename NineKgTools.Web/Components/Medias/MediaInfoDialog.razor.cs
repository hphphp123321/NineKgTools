using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Media;
using Serilog;

namespace NineKgTools.Components.Medias;

public partial class MediaInfoDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>
    /// 要展示的媒体信息
    /// </summary>
    [Parameter] public MediaBase Media { get; set; } = null!;

    /// <summary>
    /// 点击"添加到数据库"时的回调。
    /// 若传入，对话框会在内部执行并显示加载/成功/失败状态：
    /// 成功则关闭对话框并返回 Ok；失败则内联显示错误并允许用户重试。
    /// 若未传入（旧用法），则退化为直接关闭对话框返回 Ok，由调用方自己执行入库。
    /// </summary>
    [Parameter] public Func<MediaBase, Task>? OnConfirmAsync { get; set; }

    /// <summary>
    /// 自定义确认按钮文案（默认"添加到数据库"）
    /// </summary>
    [Parameter] public string ConfirmText { get; set; } = "添加到数据库";

    /// <summary>
    /// 是否隐藏"添加到数据库"按钮（用于"预览"场景）
    /// </summary>
    [Parameter] public bool HideConfirmButton { get; set; }

    private bool _isSaving;
    private string? _errorMessage;

    /// <summary>
    /// 关闭（取消）。保存中禁止关闭。
    /// </summary>
    private void Close()
    {
        if (_isSaving) return;
        MudDialog.Cancel();
    }

    /// <summary>
    /// 点击"添加到数据库"。若传入了 OnConfirmAsync 回调，则在对话框内执行并反馈状态。
    /// </summary>
    private async Task ConfirmAsync()
    {
        if (_isSaving) return;

        // 旧用法：没有回调 → 直接返回 Ok，让调用方处理
        if (OnConfirmAsync == null)
        {
            MudDialog.Close(DialogResult.Ok(true));
            return;
        }

        _errorMessage = null;
        _isSaving = true;
        StateHasChanged();

        try
        {
            await OnConfirmAsync(Media);
            // 成功 → 关闭对话框
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaInfoDialog 添加到数据库失败: {Title}", Media?.Title);
            _errorMessage = "添加到数据库失败，请稍后重试。";
            _isSaving = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 为标签生成不同颜色
    /// </summary>
    private Color GetTagColor(int index)
    {
        Color[] colors =
        {
            Color.Primary,
            Color.Secondary,
            Color.Tertiary,
            Color.Info,
            Color.Success,
            Color.Warning,
            Color.Dark
        };

        return colors[index % colors.Length];
    }
}
