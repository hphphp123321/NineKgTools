using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Core.Services.Tasks;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Dialogs;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class TaskDetailsDialog : UserControl
{
    public TaskDetailsDialog() => InitializeComponent();

    /// <summary>
    /// 弹任务详情对话框。先从 ExecutionHistory（已归档）找——字段更全，含日志；
    /// 找不到再退到 TaskProgress（运行中）；都没有时显示"任务已被清理"占位。
    /// </summary>
    public static async Task ShowAsync(
        string taskId,
        TaskProgressService progressService,
        UnifiedTaskService taskService)
    {
        ArgumentException.ThrowIfNullOrEmpty(taskId);

        var history = taskService.GetExecutionHistory()
            .FirstOrDefault(h => h.TaskId == taskId);
        var progress = progressService.GetProgress(taskId);

        var ctx = new TaskDetailsDialogContext(taskId, progress, history);
        var view = new TaskDetailsDialog { DataContext = ctx };

        var dialog = new FAContentDialog
        {
            Title = BuildTitleVisual(ctx.TaskName),
            Content = view,
            CloseButtonText = "关闭",
            DefaultButton = FAContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }

    private static Control BuildTitleVisual(string taskName)
    {
        IBrush iconBrush = ResourceLookup.Brush("SystemFillColorAttentionBrush") ?? Brushes.Gray;

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "📋",
                    FontSize = 22,
                    Foreground = iconBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                new TextBlock
                {
                    Text = "任务详情",
                    VerticalAlignment = VerticalAlignment.Center,
                },
            }
        };
    }
}
