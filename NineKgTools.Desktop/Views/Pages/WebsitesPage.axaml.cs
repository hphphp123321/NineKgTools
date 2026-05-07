using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.Views.Pages;

public partial class WebsitesPage : UserControl
{
    /// <summary>
    /// 拖拽中的 item——同窗内 reorder 不需要走 DataTransfer marshal（Avalonia 12 的
    /// IDataTransfer.Set/Contains 接 DataFormat 而非 string key；用 DataFormat.Text
    /// 又会与系统文本拖拽冲突）。直接用 class field 在 PointerPressed → Drop 之间传递
    /// 即可，简单可靠。
    /// </summary>
    private static string? _draggedItem;

    public WebsitesPage()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    private async void OnItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;
        if (control.DataContext is not string siteName) return;

        try
        {
            _draggedItem = siteName;
            // Avalonia 12 API: new DataTransfer() + Add(DataTransferItem.Create(format, value))
            // DataObject / DataFormats 已 Obsolete。实际目标识别走 _draggedItem class field
            // （仅同窗有效），DataTransfer 只为让 DoDragDropAsync 通过——装个 Text 占位
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DataFormat.Text, siteName));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "WebsitesPage 拖拽启动失败");
        }
        finally
        {
            // 清掉，防止下次误读旧值（即使 DragDrop 取消）
            _draggedItem = null;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = _draggedItem is not null ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var dragged = _draggedItem;
        if (dragged is null) return;
        if (DataContext is not WebsitesViewModel vm) return;

        // 走 visual tree 找鼠标下的 ListBoxItem，从其 DataContext 取目标站名
        if (e.Source is not Visual hit) return;
        var listBoxItem = hit.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault()
                          ?? (hit as ListBoxItem);
        if (listBoxItem?.DataContext is not string targetSite) return;
        if (string.Equals(dragged, targetSite, System.StringComparison.Ordinal)) return;

        var oldIndex = vm.PriorityItems.IndexOf(dragged);
        var newIndex = vm.PriorityItems.IndexOf(targetSite);
        if (oldIndex < 0 || newIndex < 0) return;

        vm.MovePriorityItem(oldIndex, newIndex);
        e.DragEffects = DragDropEffects.Move;
    }
}
