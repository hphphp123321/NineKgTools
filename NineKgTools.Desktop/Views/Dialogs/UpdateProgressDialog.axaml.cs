using Avalonia.Controls;
using Avalonia.Threading;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;
using Serilog;
using Velopack;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class UpdateProgressDialog : UserControl
{
    public UpdateProgressDialog() => InitializeComponent();

    /// <summary>
    /// 弹出无按钮的进度对话框并驱动"下载 → 应用 + 重启"全过程。
    /// 成功路径：下载完成后 <see cref="UpdateService.DownloadAndApplyAsync"/> 内部
    /// ApplyUpdatesAndRestart 立即退出进程，本方法不会正常返回。
    /// 失败路径：捕获异常 → 关进度框 → 弹脱敏错误确认框。
    /// </summary>
    public static async Task RunAsync(Avalonia.Visual? owner, UpdateService update, UpdateInfo info)
    {
        var version = info.TargetFullRelease?.Version?.ToString() ?? "";
        var ctx = new UpdateProgressDialogContext(version);
        var view = new UpdateProgressDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = string.IsNullOrEmpty(version) ? "正在更新" : $"正在更新到 {version}",
            Content = view,
            // 下载期不给关闭按钮——避免中途关掉对话框却仍在下载的割裂态
        };

        // 不 await：先把模态框显示出来，再并发跑下载
        var showTask = dialog.ShowAsync();
        try
        {
            await update.DownloadAndApplyAsync(
                info,
                p => Dispatcher.UIThread.Post(() => ctx.Progress = p));
            // 正常情况下 ApplyUpdatesAndRestart 已退出进程，下面不会执行
            await showTask;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "下载 / 应用更新失败");
            dialog.Hide();
            await NineKgConfirmDialog.ShowAsync(owner,
                title: "更新失败",
                message: "操作失败，请稍后重试。",
                intent: DialogIntent.Destructive,
                confirmText: "知道了");
        }
    }
}
