using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Progress;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class IdentificationProgressDialog : UserControl
{
    public IdentificationProgressDialog() => InitializeComponent();

    /// <summary>
    /// 弹出进度对话框。返回 handle：调用方在识别结束 / 失败 / 取消后 await
    /// <see cref="IdentificationProgressDialogHandle.CloseAsync"/> 关闭。
    ///
    /// 设计意图：进度对话框生命周期由 IdentificationFlowService 控制，不像普通 dialog 那样
    /// "await ShowAsync 等结果"——识别本身是 fire-and-forget 在后台跑的另一条任务。
    /// </summary>
    public static IdentificationProgressDialogHandle Show(
        DialogProgressReporter reporter,
        IdentificationDiagnostics diagnostics,
        System.Action onCancelRequested)
    {
        var ctx = new IdentificationProgressDialogContext(diagnostics);
        ctx.CancelRequested += onCancelRequested;

        System.Action<TaskLogEntry> handler = entry => ctx.HandleProgress(entry);
        reporter.OnProgress += handler;

        var view = new IdentificationProgressDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(),
            Content = view,
            // 主体里自带"取消识别"按钮——FAContentDialog 自己的三组按钮全都不需要
            DefaultButton = FAContentDialogButton.None,
        };

        // 启动显示（不 await，因为识别还要在后台跑）
        var showTask = dialog.ShowAsync();

        return new IdentificationProgressDialogHandle(
            dialog,
            ctx,
            showTask,
            () => reporter.OnProgress -= handler);
    }

    private static Control BuildTitleVisual()
    {
        IBrush iconBrush = Brushes.SteelBlue;
        if (Application.Current?.Resources.TryGetResource(
                "SystemFillColorAttentionBrush",
                Application.Current.ActualThemeVariant,
                out var brushObj) == true
            && brushObj is IBrush b)
        {
            iconBrush = b;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "🔎",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "正在识别媒体",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}

/// <summary>
/// 进度对话框 handle。控制生命周期 + 显式关闭。
/// </summary>
public sealed class IdentificationProgressDialogHandle : System.IAsyncDisposable
{
    private readonly FAContentDialog _dialog;
    private readonly IdentificationProgressDialogContext _ctx;
    private readonly System.Threading.Tasks.Task<FAContentDialogResult> _showTask;
    private readonly System.Action _detachReporter;
    private bool _closed;

    internal IdentificationProgressDialogHandle(
        FAContentDialog dialog,
        IdentificationProgressDialogContext ctx,
        System.Threading.Tasks.Task<FAContentDialogResult> showTask,
        System.Action detachReporter)
    {
        _dialog = dialog;
        _ctx = ctx;
        _showTask = showTask;
        _detachReporter = detachReporter;
    }

    /// <summary>识别结束 / 异常 / 取消后调用：关闭对话框 + 释放资源。可重复调用。</summary>
    public async System.Threading.Tasks.Task CloseAsync()
    {
        if (_closed) return;
        _closed = true;

        _ctx.Finalise();
        _detachReporter();
        _dialog.Hide();

        try
        {
            await _showTask;
        }
        catch
        {
            // FAContentDialog 内部错误不影响调用方
        }

        _ctx.Dispose();
    }

    public System.Threading.Tasks.ValueTask DisposeAsync() => new(CloseAsync());
}
