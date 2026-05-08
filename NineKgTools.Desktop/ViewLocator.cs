using System.Collections.Concurrent;
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
///
/// **View 实例缓存（key by VM Type）**：跨 tab 切换时 indicator 滑动卡顿的根因是
/// 每次 ContentControl.Content 切换都触发 ViewLocator.Build 重新 Activator.CreateInstance
/// 一个 Page UserControl，新 View 的 InitializeComponent + axaml 解析 + binding 创建 +
/// ItemsControl ItemsPanel 实例化总耗时常常 100ms+，与 selection indicator 的过渡动画
/// 抢同一根 UI 线程，indicator 因此跳帧给人"帧数低"的卡顿感。
///
/// 改用按 VM Type 缓存 View 实例后：
/// - 首次访问该 tab：照旧重建（一次性成本，用户感觉首次进入有 small hitch）
/// - 后续切回：返回 cached View，DataContext 由 ContentPresenter 自动 set 为新 VM
///   实例。Visual tree 已构建完整，仅需走一次 layout pass，UI 线程压力骤降，
///   indicator 动画拿到稳定 60fps
///
/// 安全前提：桌面端只有 MainWindow 一个 ContentControl 渲染当前页面（独立媒体详情窗
/// 走 WindowManager 不经 ViewLocator 路径），所以不会出现"同一 View 被两个 ContentPresenter
/// 同时挂载"的非法状态。
/// </summary>
public class ViewLocator : IDataTemplate
{
    private readonly ConcurrentDictionary<Type, Control> _viewCache = new();

    public Control Build(object? data)
    {
        if (data is null) return new TextBlock { Text = "(null)" };

        var vmType = data.GetType();

        // 命中缓存 → 直接返回；DataContext 切换由 ContentPresenter 处理
        if (_viewCache.TryGetValue(vmType, out var cached))
            return cached;

        var viewName = vmType.FullName!
            .Replace("ViewModels", "Views", StringComparison.Ordinal)
            .Replace("ViewModel", "Page", StringComparison.Ordinal);

        // 用 VM 所在的 Assembly 解析 View 类型——比 Type.GetType(string) 更可靠
        var viewType = vmType.Assembly.GetType(viewName);
        if (viewType is null)
        {
            Log.Warning("ViewLocator 找不到 View 类型：{ViewName}（命名约定 ViewModels.XxxViewModel → Views.XxxPage）", viewName);
            // 错误占位不缓存：用户修了命名后重启就生效
            return new TextBlock
            {
                Text = $"⚠ View 未找到：{viewName}",
                Margin = new Avalonia.Thickness(32),
                Foreground = Avalonia.Media.Brushes.OrangeRed,
            };
        }

        try
        {
            var view = (Control)Activator.CreateInstance(viewType)!;
            _viewCache[vmType] = view;
            return view;
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
