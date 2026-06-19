using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class FirstRunWizardDialog : UserControl
{
    public FirstRunWizardDialog() => InitializeComponent();

    /// <summary>
    /// 弹出首次启动 3 步引导（无标准按钮，靠内部 上一步/跳过/下一步/完成 导航）。
    /// 完成 / 跳过都会 RequestClose 关闭；调用方在返回后落 FirstRunCompleted=true。
    /// </summary>
    public static async Task ShowAsync(Config config, FilesService files, string dataDir)
    {
        var ctx = new FirstRunWizardContext(config, files, dataDir);
        var view = new FirstRunWizardDialog { DataContext = ctx };
        var dialog = new FAContentDialog
        {
            Title = "欢迎使用 NineKgTools",
            Content = view,
        };
        // 内容比默认 ContentDialogMaxWidth(~548) 略宽，撑大外框避免裁切（与选择器对话框同理）
        dialog.Resources["ContentDialogMaxWidth"] = 640d;
        ctx.RequestClose += () => dialog.Hide();
        await dialog.ShowAsync();
    }

    private async void OnAddFolderClick(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top?.StorageProvider is null) return;

        var picked = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择要监视的文件夹",
            AllowMultiple = false,
        });
        var path = picked.FirstOrDefault()?.TryGetLocalPath();
        if (DataContext is FirstRunWizardContext ctx)
            await ctx.TryAddWatchFolderAsync(path);
    }
}
