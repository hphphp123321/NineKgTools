using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.Views.Pages;

public partial class SourcesPage : UserControl
{
    public SourcesPage()
    {
        InitializeComponent();

        // 启用页面级拖拽接收（吃掉事件，外层主窗 DragOverlay 不会再 hover）
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DataFormat.File)) return;
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;  // 阻止冒泡到主窗 OnDragEnter，由本页 hover 反馈替代

        if (DataContext is SourcesViewModel vm)
            vm.IsDragOver = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        if (DataContext is SourcesViewModel vm)
            vm.IsDragOver = false;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DataContext is SourcesViewModel vm)
            vm.IsDragOver = false;

        var paths = DragDropDispatcher.ExtractLocalPaths(e);
        if (paths.Count == 0) return;

        try
        {
            // dispatcher 是 Singleton，提交后通过 TaskSubmitted 事件通知 SourcesViewModel
            // —— ViewModel 在 OnEnter 时已订阅，会自动加进 TrackedTasks
            if (DataContext is SourcesViewModel viewModel)
            {
                await viewModel.HandleDroppedPathsAsync(paths);
            }
            else
            {
                // 防御性 fallback：DataContext 还没绑定时直接 fallback 到 dispatcher
                var dispatcher = Program.Services.GetRequiredService<DragDropDispatcher>();
                await dispatcher.HandleDropAsync(paths);
            }
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "SourcesPage 处理拖拽失败");
        }
    }
}
