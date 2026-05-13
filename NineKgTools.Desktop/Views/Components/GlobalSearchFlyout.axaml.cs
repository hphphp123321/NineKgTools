using Avalonia.Controls;
using Avalonia.Input;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop.Views.Components;

public partial class GlobalSearchFlyout : UserControl
{
    public GlobalSearchFlyout()
    {
        InitializeComponent();
    }

    /// <summary>条目点击——通过 Tag 拿绑定的 FlyoutSearchItem，路由到 VM.ActivateEntryAsync。</summary>
    private async void OnItemClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border b) return;
        if (b.Tag is not FlyoutSearchItem item) return;
        if (DataContext is not GlobalSearchFlyoutViewModel vm) return;
        try
        {
            await vm.ActivateEntryAsync(item);
        }
        catch (System.Exception ex)
        {
            Log.Warning(ex, "GlobalSearchFlyout 条目点击失败");
        }
    }
}
