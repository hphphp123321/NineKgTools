using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop;

/// <summary>
/// 把 ViewModel 自动映射到对应的 View（UserControl）。
/// 命名约定：`NineKgTools.Desktop.ViewModels.Pages.XxxViewModel`
///        →  `NineKgTools.Desktop.Views.Pages.XxxPage`
/// 在 App.axaml 的 Application.DataTemplates 引入，使 ContentControl
/// 绑定 ViewModel 时自动渲染对应 View。
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "(null)" };

        var vmType = data.GetType();
        var viewName = vmType.FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "Page", StringComparison.Ordinal);

        // 用 VM 所在的 Assembly 解析 View 类型——比 Type.GetType(string) 更可靠
        var viewType = vmType.Assembly.GetType(viewName);
        if (viewType is null)
        {
            Log.Warning("ViewLocator 找不到 View 类型：{ViewName}（命名约定 ViewModels.XxxViewModel → Views.XxxPage）", viewName);
            return new TextBlock
            {
                Text = $"⚠ View 未找到：{viewName}",
                Margin = new Avalonia.Thickness(32),
                Foreground = Avalonia.Media.Brushes.OrangeRed,
            };
        }

        try
        {
            return (Control)Activator.CreateInstance(viewType)!;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ViewLocator.Build: 实例化 {ViewName} 失败", viewName);
            return new TextBlock
            {
                Text = $"❌ View 实例化失败：{viewName}\n{ex.Message}",
                Margin = new Avalonia.Thickness(32),
                Foreground = Avalonia.Media.Brushes.OrangeRed,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
        }
    }

    public bool Match(object? data) => data is PageViewModelBase;
}
