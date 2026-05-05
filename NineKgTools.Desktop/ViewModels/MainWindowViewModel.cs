using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels.Pages;

namespace NineKgTools.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly NavigationService _nav;

    [ObservableProperty]
    private PageViewModelBase? _currentPage;

    public MainWindowViewModel(NavigationService nav)
    {
        _nav = nav;
        _nav.CurrentPageChanged += (_, page) => CurrentPage = page;
    }

    /// <summary>窗口加载后调用，导航到首页。</summary>
    public Task InitializeAsync() => _nav.NavigateToAsync<HomeViewModel>();

    /// <summary>由 MainWindow 的 NavigationView SelectionChanged 调用。</summary>
    public Task NavigateAsync(Type viewModelType) => _nav.NavigateToAsync(viewModelType);
}
