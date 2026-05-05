using CommunityToolkit.Mvvm.ComponentModel;

namespace NineKgTools.Desktop.ViewModels;

/// <summary>
/// 所有可导航页面 VM 的基类。提供生命周期钩子：
/// - OnEnterAsync：被导航到时调用（应在此加载数据）
/// - OnLeaveAsync：导航离开时调用（应在此释放订阅、保存状态）
/// </summary>
public abstract partial class PageViewModelBase : ObservableObject
{
    /// <summary>页面标题，显示在主窗口或 NavigationView 选中态</summary>
    public abstract string Title { get; }

    /// <summary>导航进入时调用。默认空实现，子类按需 override。</summary>
    public virtual Task OnEnterAsync() => Task.CompletedTask;

    /// <summary>导航离开时调用。默认空实现，子类按需 override。</summary>
    public virtual Task OnLeaveAsync() => Task.CompletedTask;
}
