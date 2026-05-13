using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly NavigationService _nav;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    /// <summary>侧栏底部全局搜索框对应的 Flyout VM——MainWindow.axaml 的 SearchBox.Text 绑到 SearchFlyoutVm.Query；
    /// SearchBox.GotFocus → IsOpen=true；键盘 ↑↓/Enter/Esc/Ctrl+Enter 路由到 Flyout VM 方法。</summary>
    public GlobalSearchFlyoutViewModel SearchFlyoutVm { get; }

    public MainWindowViewModel(NavigationService nav, GlobalSearchFlyoutViewModel searchFlyoutVm)
    {
        _nav = nav;
        SearchFlyoutVm = searchFlyoutVm;
        _nav.CurrentPageChanged += (_, page) => CurrentPage = page;
    }

    /// <summary>窗口加载后调用，导航到首页。</summary>
    public Task InitializeAsync() => _nav.NavigateToAsync<HomeViewModel>();

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
