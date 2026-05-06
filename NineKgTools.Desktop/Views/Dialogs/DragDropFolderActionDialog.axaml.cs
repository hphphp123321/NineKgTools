using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.Views.Dialogs;

public partial class DragDropFolderActionDialog : UserControl
{
    public static readonly StyledProperty<string> FolderPathProperty =
        AvaloniaProperty.Register<DragDropFolderActionDialog, string>(nameof(FolderPath), "");

    public static readonly StyledProperty<string> ItemCountTextProperty =
        AvaloniaProperty.Register<DragDropFolderActionDialog, string>(nameof(ItemCountText), "");

    public string FolderPath
    {
        get => GetValue(FolderPathProperty);
        set => SetValue(FolderPathProperty, value);
    }

    public string ItemCountText
    {
        get => GetValue(ItemCountTextProperty);
        set => SetValue(ItemCountTextProperty, value);
    }

    private FolderDragAction? _result;
    private ContentDialog? _ownerDialog;

    public DragDropFolderActionDialog() => InitializeComponent();

    /// <summary>
    /// 弹出"加入监视 / 一次性识别"双卡片对话框；返回 null = 用户取消。
    /// </summary>
    public static async Task<FolderDragAction?> ShowAsync(string folderPath)
    {
        int fileCount = 0;
        try
        {
            fileCount = Directory.EnumerateFiles(folderPath, "*", SearchOption.TopDirectoryOnly).Count();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DragDrop 统计文件夹文件数失败：{Path}", folderPath);
        }

        var view = new DragDropFolderActionDialog
        {
            FolderPath = folderPath,
            ItemCountText = fileCount > 0 ? $"包含 {fileCount} 个文件（顶层）" : "（空文件夹）",
        };

        var dialog = new ContentDialog
        {
            Title = BuildTitle(),
            Content = view,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None,
        };
        view._ownerDialog = dialog;

        await dialog.ShowAsync();
        return view._result;
    }

    private void OnAddToWatchClicked(object? sender, RoutedEventArgs e)
    {
        _result = FolderDragAction.AddToWatch;
        _ownerDialog?.Hide();
    }

    private void OnIdentifyOnceClicked(object? sender, RoutedEventArgs e)
    {
        _result = FolderDragAction.IdentifyOnce;
        _ownerDialog?.Hide();
    }

    private static Control BuildTitle()
    {
        IBrush iconBrush = Brushes.SteelBlue;
        if (Application.Current?.Resources.TryGetResource(
                "AccentFillColorDefaultBrush", Application.Current.ActualThemeVariant, out var b) == true
            && b is IBrush br)
        {
            iconBrush = br;
        }

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "📁", FontSize = 22, Foreground = iconBrush, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = "你拖入了一个文件夹", VerticalAlignment = VerticalAlignment.Center },
            }
        };
    }
}
