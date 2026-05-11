using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.ViewModels;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 主窗内的页面导航。从 DI 解析 ViewModel 实例，触发 CurrentPage 切换。
/// 多窗口（如媒体详情独立窗）走 WindowManager（Phase 3 引入），不在此服务。
///
/// **历史栈**（v2 升级）：每次 NavigateToAsync 把旧 _currentPage 压入 _history；
/// <see cref="NavigateBackAsync"/> 弹出栈顶恢复为 CurrentPage（不触发 OnEnterAsync——
/// 用户期望"返回"看到的是历史快照而非重新加载）。
/// </summary>
public class NavigationService
{
    private readonly IServiceProvider _services;
    private PageViewModelBase? _currentPage;
    private readonly Stack<PageViewModelBase> _history = new();

    public event EventHandler<PageViewModelBase?>? CurrentPageChanged;

    /// <summary>历史栈深度变化（push / pop）时触发——UI 用它刷新 [← 返回] 按钮 IsEnabled</summary>
    public event EventHandler? CanGoBackChanged;

    public PageViewModelBase? CurrentPage => _currentPage;

    /// <summary>是否可后退（历史栈非空）。MediaDetailPage 顶部 [← 返回] 按钮 IsEnabled 用此值</summary>
    public bool CanGoBack => _history.Count > 0;

    public NavigationService(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// 导航到指定 ViewModel 类型。每次调用都会创建新实例（Transient）；
    /// 旧 VM 的 OnLeaveAsync 会被调用，新 VM 的 OnEnterAsync 触发数据加载。
    /// 旧 VM 进历史栈。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>() where TViewModel : PageViewModelBase
    {
        await NavigateToAsync(typeof(TViewModel));
    }

    /// <summary>
    /// 导航到指定 ViewModel 并在 OnEnterAsync 之前执行一次配置回调。
    /// 用于把"全局搜索关键词" / "媒体 mediaId"等参数注入新 VM——配置发生在 OnEnter 之前，
    /// 所以 VM 的 OnEnterAsync 可以读取到这些值（如 SearchText / _pendingMediaId）来执行首次加载。
    /// </summary>
    public async Task NavigateToAsync<TViewModel>(Action<TViewModel>? configureBeforeEnter)
        where TViewModel : PageViewModelBase
    {
        // 旧页面 leave + 入栈
        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
            _history.Push(_currentPage);
        }

        // 新页面：先 configure 再 enter，确保 OnEnterAsync 能读到注入的参数
        var newPage = _services.GetRequiredService<TViewModel>();
        try { configureBeforeEnter?.Invoke(newPage); }
        catch (Exception ex) { Log.Error(ex, "configureBeforeEnter 失败：{Type}", typeof(TViewModel)); }

        try { await newPage.OnEnterAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", typeof(TViewModel)); }

        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);
        CanGoBackChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task NavigateToAsync(Type viewModelType)
    {
        if (!typeof(PageViewModelBase).IsAssignableFrom(viewModelType))
        {
            Log.Warning("NavigateTo 拒绝非 PageViewModelBase 类型：{Type}", viewModelType);
            return;
        }

        // 旧页面 leave + 入栈
        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
            _history.Push(_currentPage);
        }

        // 新页面 enter
        var newPage = (PageViewModelBase)_services.GetRequiredService(viewModelType);
        try { await newPage.OnEnterAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", viewModelType); }

        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);
        CanGoBackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 后退一页。把当前页 OnLeave 后丢弃（不进栈——单向历史），栈顶 popped 恢复为 CurrentPage。
    /// **不**触发 popped VM 的 OnEnterAsync——用户期望"返回"看历史快照而非重 load。
    /// 栈空时静默 no-op（UI 的按钮 IsEnabled 应已根据 <see cref="CanGoBack"/> 禁用，是双保险）。
    /// </summary>
    public async Task NavigateBackAsync()
    {
        if (_history.Count == 0)
        {
            Log.Debug("NavigateBackAsync: 历史栈空，no-op");
            return;
        }

        // 当前页 leave 后丢弃（不再 push 回栈——避免无限循环）
        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
        }

        var prev = _history.Pop();
        _currentPage = prev;
        CurrentPageChanged?.Invoke(this, prev);
        CanGoBackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>清空历史栈——主窗顶部 NavigationView 主菜单切换时调用，
    /// 让"返回栈"重新基于新的根页面（防止从"待识别" ← 退到"媒体库"这种跨主菜单退回）</summary>
    public void ClearHistory()
    {
        if (_history.Count == 0) return;
        _history.Clear();
        CanGoBackChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// "根级"导航——主菜单切换用。**不 push** 当前页到历史栈 + 清空已有历史，
    /// 让新页成为新的根：用户在新页上 ← 返回不会退到之前的主菜单页（这是横向跳，不该有 history）。
    /// </summary>
    public async Task NavigateAsRootAsync(Type viewModelType)
    {
        if (!typeof(PageViewModelBase).IsAssignableFrom(viewModelType))
        {
            Log.Warning("NavigateAsRoot 拒绝非 PageViewModelBase 类型：{Type}", viewModelType);
            return;
        }

        if (_currentPage is not null)
        {
            try { await _currentPage.OnLeaveAsync(); }
            catch (Exception ex) { Log.Error(ex, "OnLeaveAsync 失败：{Type}", _currentPage.GetType()); }
            // 注意：不 push 到 _history——根级跳转抛弃当前页栈
        }

        var hadHistory = _history.Count > 0;
        _history.Clear();

        var newPage = (PageViewModelBase)_services.GetRequiredService(viewModelType);
        try { await newPage.OnEnterAsync(); }
        catch (Exception ex) { Log.Error(ex, "OnEnterAsync 失败：{Type}", viewModelType); }

        _currentPage = newPage;
        CurrentPageChanged?.Invoke(this, newPage);
        if (hadHistory) CanGoBackChanged?.Invoke(this, EventArgs.Empty);
    }
}
