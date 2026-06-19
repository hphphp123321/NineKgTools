using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;
using Velopack;

namespace NineKgTools.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly NavigationService _nav;
    private readonly UpdateService _update;
    private readonly DesktopPreferences _preferences;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    /// <summary>侧栏底部全局搜索框对应的 Flyout VM——MainWindow.axaml 的 SearchBox.Text 绑到 SearchFlyoutVm.Query；
    /// SearchBox.GotFocus → IsOpen=true；键盘 ↑↓/Enter/Esc/Ctrl+Enter 路由到 Flyout VM 方法。</summary>
    public GlobalSearchFlyoutViewModel SearchFlyoutVm { get; }

    // ===== 自动更新（顶部 InfoBar）=====
    /// <summary>有新版本可用 → 主窗顶部 FAInfoBar 显示。仅 Velopack 安装版会被置 true。</summary>
    [ObservableProperty]
    private bool _updateAvailable;

    /// <summary>可用新版本号（InfoBar 文案用）。</summary>
    [ObservableProperty]
    private string _updateVersionText = "";

    /// <summary>静默检查命中的待应用更新。点"立即更新"时消费。</summary>
    private UpdateInfo? _pendingUpdate;

    public MainWindowViewModel(
        NavigationService nav,
        GlobalSearchFlyoutViewModel searchFlyoutVm,
        UpdateService update,
        DesktopPreferences preferences)
    {
        _nav = nav;
        _update = update;
        _preferences = preferences;
        SearchFlyoutVm = searchFlyoutVm;
        _nav.CurrentPageChanged += (_, page) => CurrentPage = page;
    }

    /// <summary>窗口加载后调用，导航到首页，并按偏好静默检查更新（不阻塞 UI）。</summary>
    public async Task InitializeAsync()
    {
        await _nav.NavigateToAsync<HomeViewModel>();
        if (_preferences.AutoCheckUpdates)
        {
            // 不 await：检查失败 / 无更新都静默，绝不打断启动
            _ = CheckUpdatesSilentlyAsync();
        }
    }

    private async Task CheckUpdatesSilentlyAsync()
    {
        try
        {
            var info = await _update.CheckAsync();
            _preferences.LastUpdateCheck = DateTime.UtcNow;
            _preferences.RequestSave();
            if (info?.TargetFullRelease is { } rel)
            {
                _pendingUpdate = info;
                UpdateVersionText = rel.Version?.ToString() ?? "";
                UpdateAvailable = true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "静默检查更新失败（忽略）");
        }
    }

    /// <summary>InfoBar "立即更新"：弹进度对话框下载并应用（应用阶段进程退出重启）。
    /// "稍后" = InfoBar 自带的可关闭 X（IsClosable，TwoWay 写回 UpdateAvailable=false）。</summary>
    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (_pendingUpdate is null) return;
        UpdateAvailable = false;
        await UpdateProgressDialog.RunAsync(null, _update, _pendingUpdate);
    }

    /// <summary>由 MainWindow 的 NavigationView SelectionChanged 调用。
    /// 走 NavigateAsRootAsync —— 主菜单是"横向跳"语义，不该进历史栈。</summary>
    public Task NavigateAsync(Type viewModelType) => _nav.NavigateAsRootAsync(viewModelType);

    /// <summary>Ctrl+Enter / SearchBox 按 Enter（无高亮项时） → 跳完整 SearchResultPage。委托给 Flyout VM 的 ViewAll。</summary>
    [RelayCommand]
    private async Task ExecuteSearchAsync()
    {
        await SearchFlyoutVm.ViewAllAsync();
    }
}
