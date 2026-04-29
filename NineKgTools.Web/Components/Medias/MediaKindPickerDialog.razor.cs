using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace NineKgTools.Components.Medias;

/// <summary>
/// 新建媒体时的"文件夹 / 单文件"可视化选择器。
/// 替代原先的 3-way <c>ShowMessageBox</c>（yes/no/cancel）。
/// 点击卡片即选择并关闭；没有"确认"按钮，取消仍保留作为逃生口。
/// </summary>
public partial class MediaKindPickerDialog : ComponentBase
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    private void SelectFolder() => MudDialog.Close(DialogResult.Ok(MediaKind.Folder));
    private void SelectFile() => MudDialog.Close(DialogResult.Ok(MediaKind.File));
    private void Cancel() => MudDialog.Cancel();

    // WAI-ARIA button 模式：Enter / Space 等同 click。
    // foreach 没用到，但两个卡片都是 button，保持键盘可达。
    private void OnCardKeyDown(KeyboardEventArgs e, MediaKind kind)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
        {
            if (kind == MediaKind.Folder) SelectFolder();
            else SelectFile();
        }
    }

    /// <summary>
    /// 便利方法：一行代码弹出选择器，返回用户的选择（取消时返回 null）。
    /// </summary>
    public static async Task<MediaKind?> ShowAsync(IDialogService dialogService)
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true,
            CloseButton = true,
            Position = DialogPosition.Center
        };

        var dialog = await dialogService.ShowAsync<MediaKindPickerDialog>("新建媒体", options);
        var result = await dialog.Result;
        return result is { Canceled: false, Data: MediaKind kind } ? kind : null;
    }
}

/// <summary>
/// 新建媒体时用户选择的类别：一个文件夹 / 一个单独文件。
/// 对应原 <c>FileExplorer</c> 的 <c>FileSelectMode</c>，但独立定义以便调用方 switch。
/// </summary>
public enum MediaKind
{
    Folder,
    File
}
