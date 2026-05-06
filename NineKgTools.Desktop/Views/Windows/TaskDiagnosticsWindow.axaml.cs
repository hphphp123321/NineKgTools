using Avalonia.Controls;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Desktop.ViewModels.Components;

namespace NineKgTools.Desktop.Views.Windows;

/// <summary>
/// 显示一次识别任务诊断的独立窗口。Owner 应该是 MainWindow 以保证 z-order。
/// </summary>
public partial class TaskDiagnosticsWindow : Window
{
    public TaskDiagnosticsWindow() => InitializeComponent();

    public TaskDiagnosticsWindow(IdentificationDiagnostics? diagnostics, string? taskName = null) : this()
    {
        DataContext = new IdentificationDiagnosticsViewModel(diagnostics);
        if (!string.IsNullOrEmpty(taskName))
        {
            Title = $"识别诊断 · {taskName}";
        }
    }
}
