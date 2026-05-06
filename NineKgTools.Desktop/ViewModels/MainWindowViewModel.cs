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

    /// <summary>主窗顶部 / 侧栏底部的全局搜索框输入。Enter 时触发 ExecuteSearchCommand。</summary>
    [ObservableProperty]
    private string _searchText = "";

    public MainWindowViewModel(NavigationService nav)
    {
        _nav = nav;
        _nav.CurrentPageChanged += (_, page) => CurrentPage = page;
    }

    /// <summary>窗口加载后调用，导航到首页。</summary>
    public Task InitializeAsync() => _nav.NavigateToAsync<HomeViewModel>();

    /// <summary>由 MainWindow 的 NavigationView SelectionChanged 调用。</summary>
    public Task NavigateAsync(Type viewModelType) => _nav.NavigateToAsync(viewModelType);

    /// <summary>
    /// 全局搜索：导航到媒体库并以输入的关键词预填搜索框，OnEnterAsync 自动加载结果。
    /// 关键词为空时也允许（等价于"清空搜索 + 跳到媒体库"）。
    /// </summary>
    [RelayCommand]
    private async Task ExecuteSearchAsync()
    {
        var term = SearchText?.Trim() ?? "";
        await _nav.NavigateToAsync<MediaOverviewViewModel>(vm =>
        {
            vm.SearchText = term;
        });
    }
}

