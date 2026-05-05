using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnNavigationSelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        Type? targetType = null;

        if (args.IsSettingsSelected)
        {
            targetType = typeof(SettingsViewModel);
        }
        else if (args.SelectedItem is NavigationViewItem item && item.Tag is string typeName)
        {
            // Tag 形如 "HomeViewModel"，命名空间在 NineKgTools.Desktop.ViewModels.Pages
            targetType = Type.GetType($"NineKgTools.Desktop.ViewModels.Pages.{typeName}, NineKgTools.Desktop");
        }

        if (targetType is null) return;

        try
        {
            await vm.NavigateAsync(targetType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "导航失败 → {Type}", targetType);
        }
    }
}
