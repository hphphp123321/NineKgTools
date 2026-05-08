using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 主窗内的页面导航。从 DI 解析 ViewModel 实例，触发 CurrentPage 切换。
/// 多窗口（如媒体详情独立窗）走 WindowManager（Phase 3 引入），不在此服务。
///
/// **OnEnterAsync 的时序设计**（动画流畅度的关键）：
/// 旧实现里 `await newPage.OnEnterAsync()` 同步等待数据加载完成才发
/// CurrentPageChanged。但很多 VM 的 OnEnterAsync 内部走了 UI 线程同步的 EF 查询
/// （DbContext 非线程安全，Includes 的分页查询不能 Task.Run），这会冻结 NavigationView
/// 的 selection indicator 动画——跨多 tab 跳转时尤其明显，indicator 沿轨道滑过时
/// 中途卡顿。
///
/// 改为：先 emit CurrentPageChanged 让 ContentControl 立即换页 + indicator 动画
/// 启动；再在 Background 优先级的 dispatcher post 里执行 OnEnterAsync，让首帧
/// 渲染先跑完。新 View 在数据回来之前先以"空内容 / IsLoading=true"渲染（每个 VM
/// 的 default 状态都是空集合），动画结束后看到的就是数据已填充的页面。
/// </summary>
public class NavigationService
{
    private readonly IServiceProvider _services;
    private PageViewModelBase? _currentPage;

    public event EventHandler<PageViewModelBase?>? CurrentPageChanged;

    public PageViewModelBase? CurrentPage => _currentPage;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// 导航到指定 ViewModel 类型。每次调用都会创建新实例（Transient）；
    /// 旧 VM 的 OnLeaveAsync 会被调用，新 VM 的 OnEnterAsync 在下一帧执行（避免阻塞 indicator 动画）。
    /// </summary>
    public Task NavigateToAsync<TViewModel>() where TViewModel : PageViewModelBase
        => NavigateToAsync(typeof(TViewModel));

    /// <summary>
    /// 导航到指定 ViewModel 并在 OnEnterAsync 之前执行一次配置回调。
    /// 用于把"全局搜索关键词"等参数注入新 VM——configureBeforeEnter 是同步调用，
    /// 在 OnEnterAsync 推迟执行前完成；所以 VM 的 OnEnterAsync 可以读取到这些值。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>(Action<TViewModel>? configureBeforeEnter)
        where TViewModel : PageViewModelBase
    {
        await LeaveCurrentAsync();

        var newPage = _services.GetRequiredService<TViewModel>();
        try { configureBeforeEnter?.Invoke(newPage); }
        catch (Exception ex) { Log.Error(ex, "configureBeforeEnter 失败：{Type}", typeof(TViewModel)); }

        SwitchToAndDeferEnter(newPage);
    }

    public async Task NavigateToAsync(Type viewModelType)
    {
        if (!typeof(PageViewModelBase).IsAssignableFrom(viewModelType))
        {
            Log.Warning("NavigateTo 拒绝非 PageViewModelBase 类型：{Type}", viewModelType);
            return;
        }

        await LeaveCurrentAsync();

        var newPage = (PageViewModelBase)_services.GetRequiredService(viewModelType);
        SwitchToAndDeferEnter(newPage);
    }

    private async Task LeaveCurrentAsync()
    {
        if (_currentPage is null) return;
        try { await _currentPage.OnLeaveAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
    }

    /// <summary>
    /// 立刻把 _currentPage 切到 newPage 并 emit CurrentPageChanged（让侧栏 indicator
    /// 滑动 + ContentControl 创建新 View 立即开始），再用 Background 优先级 post
    /// OnEnterAsync——保证 indicator 动画与首帧 layout 在 UI 线程上有空跑完。
    /// </summary>
    private void SwitchToAndDeferEnter(PageViewModelBase newPage)
    {
        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);

        Dispatcher.UIThread.Post(async () =>
        {
            try { await newPage.OnEnterAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", newPage.GetType()); }
        }, DispatcherPriority.Background);
    }
}
