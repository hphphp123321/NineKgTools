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
    /// 全局搜索：导航到 SearchResultPage，预填 Query 触发 4 类型搜索（媒体 / 标签 / 创作者 / 社团）。
    /// AI 语义搜索由用户在结果页 ToggleSwitch 控制。
    /// </summary>
    [RelayCommand]
    private async Task ExecuteSearchAsync()
    {
        var term = SearchText?.Trim() ?? "";
        if (string.IsNullOrEmpty(term)) return;

        await _nav.NavigateToAsync<SearchResultViewModel>(vm =>
        {
            vm.Query = term;
        });
    }
}

