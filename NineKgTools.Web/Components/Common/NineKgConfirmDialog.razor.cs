using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NineKgTools.Components.Common;

/// <summary>
/// 共享确认对话框。替代项目中全部 <c>DialogService.ShowMessageBox</c> 调用。
/// 支持四种 Intent 变体，自动驱动 Hero 渐变色、accent 色条、按钮色和默认图标。
/// </summary>
public partial class NineKgConfirmDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Message { get; set; } = string.Empty;

    [Parameter] public ConfirmIntent Intent { get; set; } = ConfirmIntent.Info;

    /// <summary>Hero 副标，常用于补充说明（如"确认删除 · 可能影响外部引用"）。留空不显示。</summary>
    [Parameter] public string? HeroSubtitle { get; set; }

    /// <summary>
    /// 被操作对象名。仅 <see cref="ConfirmIntent.Destructive"/> 时生效，会在正文上方渲染一张卡片预览。
    /// </summary>
    [Parameter] public string? TargetName { get; set; }

    /// <summary>目标卡片图标，默认 Delete。</summary>
    [Parameter] public string? TargetIcon { get; set; }

    /// <summary>批量操作时受影响的记录数。仅 <see cref="ConfirmIntent.DestructiveBatch"/> 时在 Hero 渲染大数字。</summary>
    [Parameter] public int? AffectedCount { get; set; }

    [Parameter] public string ConfirmText { get; set; } = "确定";
    [Parameter] public string CancelText { get; set; } = "取消";

    /// <summary>覆盖默认的 Hero 图标。</summary>
    [Parameter] public string? Icon { get; set; }

    /// <summary>覆盖默认的"此操作不可撤销"警告行文案（仅 Destructive 类有效）。</summary>
    [Parameter] public string? WarningLine { get; set; }

    private string ResolvedIcon => Icon ?? Intent switch
    {
        ConfirmIntent.Info => Icons.Material.Filled.Info,
        ConfirmIntent.Affirmative => Icons.Material.Filled.CheckCircle,
        ConfirmIntent.Destructive => Icons.Material.Filled.DeleteForever,
        ConfirmIntent.DestructiveBatch => Icons.Material.Filled.WarningAmber,
        _ => Icons.Material.Filled.Info
    };

    private string IntentSuffix => Intent switch
    {
        ConfirmIntent.Info => "info",
        ConfirmIntent.Affirmative => "success",
        ConfirmIntent.Destructive or ConfirmIntent.DestructiveBatch => "error",
        _ => "info"
    };

    private string FrameCssClass => $"nk-dialog-frame nk-dialog-frame--{IntentSuffix}";
    private string HeroCssClass => $"nk-dialog-hero nk-dialog-hero--{IntentSuffix}";

    private Color ConfirmButtonColor => Intent switch
    {
        ConfirmIntent.Info => Color.Primary,
        ConfirmIntent.Affirmative => Color.Success,
        ConfirmIntent.Destructive or ConfirmIntent.DestructiveBatch => Color.Error,
        _ => Color.Primary
    };

    private void Confirm() => MudDialog.Close(DialogResult.Ok(true));
    private void Cancel() => MudDialog.Cancel();

    /// <summary>
    /// 便利方法：一行代码弹出确认框，返回用户是否点击了确认。
    /// 内部用 <see cref="MaxWidth.ExtraSmall"/> + FullWidth，尺寸与一般 MessageBox 一致。
    /// </summary>
    public static async Task<bool> ShowAsync(
        IDialogService dialogService,
        string title,
        string message,
        ConfirmIntent intent = ConfirmIntent.Info,
        string confirmText = "确定",
        string cancelText = "取消",
        string? targetName = null,
        string? targetIcon = null,
        int? affectedCount = null,
        string? icon = null,
        string? heroSubtitle = null,
        string? warningLine = null)
    {
        var parameters = new DialogParameters<NineKgConfirmDialog>
        {
            { x => x.Title, title },
            { x => x.Message, message },
            { x => x.Intent, intent },
            { x => x.ConfirmText, confirmText },
            { x => x.CancelText, cancelText },
            { x => x.TargetName, targetName },
            { x => x.TargetIcon, targetIcon },
            { x => x.AffectedCount, affectedCount },
            { x => x.Icon, icon },
            { x => x.HeroSubtitle, heroSubtitle },
            { x => x.WarningLine, warningLine }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center,
            BackdropClick = false
        };

        var dialog = await dialogService.ShowAsync<NineKgConfirmDialog>(title, parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: true };
    }
}

/// <summary>
/// 确认对话框意图。决定 Hero 渐变起始色、accent 色条、主按钮色、默认图标、警告行是否显示。
/// </summary>
public enum ConfirmIntent
{
    /// <summary>一般信息确认（通知同步、信息确认）—— primary 色。</summary>
    Info,

    /// <summary>积极操作确认（入库、加入队列、启动同步）—— success 色。</summary>
    Affirmative,

    /// <summary>单对象破坏性操作（删除媒体/标签/映射 等）—— error 色，正文上方显示 TargetName 卡片。</summary>
    Destructive,

    /// <summary>批量破坏性操作 —— error 色，Hero 显示大号 AffectedCount。</summary>
    DestructiveBatch
}
