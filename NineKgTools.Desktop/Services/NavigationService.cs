using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 主窗内的页面导航。从 DI 解析 ViewModel 实例，触发 CurrentPage 切换。
/// 多窗口（如媒体详情独立窗）走 WindowManager（Phase 3 引入），不在此服务。
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
    /// 旧 VM 的 OnLeaveAsync 会被调用，新 VM 的 OnEnterAsync 触发数据加载。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>() where TViewModel : PageViewModelBase
    {
        await NavigateToAsync(typeof(TViewModel));
    }

    /// <summary>
    /// 导航到指定 ViewModel 并在 OnEnterAsync 之前执行一次配置回调。
    /// 用于把"全局搜索关键词"等参数注入新 VM——配置发生在 OnEnter 之前，
    /// 所以 VM 的 OnEnterAsync 可以读取到这些值（如 SearchText）来执行首次加载。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>(Action<TViewModel>? configureBeforeEnter)
        where TViewModel : PageViewModelBase
    {
        // 旧页面 leave
        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
        }

        // 新页面：先 configure 再 enter，确保 OnEnterAsync 能读到注入的参数
        var newPage = _services.GetRequiredService<TViewModel>();
        try { configureBeforeEnter?.Invoke(newPage); }
        catch (Exception ex) { Log.Error(ex, "configureBeforeEnter 失败：{Type}", typeof(TViewModel)); }

        try { await newPage.OnEnterAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", typeof(TViewModel)); }

        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);
    }

    public async Task NavigateToAsync(Type viewModelType)
    {
        if (!typeof(PageViewModelBase).IsAssignableFrom(viewModelType))
        {
            Log.Warning("NavigateTo 拒绝非 PageViewModelBase 类型：{Type}", viewModelType);
            return;
        }

        // 旧页面 leave
        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
        }

        // 新页面 enter
        var newPage = (PageViewModelBase)_services.GetRequiredService(viewModelType);
        try { await newPage.OnEnterAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", viewModelType); }

        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);
    }
}
