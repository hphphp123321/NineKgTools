using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Services.Progress;
using NineKgTools.Components.Tasks;

namespace NineKgTools.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private TaskProgressService ProgressService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private HttpClient HttpClient { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    // 任务Badge相关
    private int _activeTaskCount = 0;
    private System.Threading.Timer? _badgeRefreshTimer;

    // 主题模式枚举
    private enum ThemeMode { Dark, Purple, Light }

    // 主题相关
    private ThemeMode _currentTheme = ThemeMode.Dark;
    public bool IsDarkMode => _currentTheme != ThemeMode.Light;

    private MudTheme? _theme = null;
    private MudTheme? _purpleTheme = null;
    private MudTheme CurrentTheme => _currentTheme == ThemeMode.Purple ? _purpleTheme! : _theme!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // 初始化默认主题（深色 + 浅色）
        _theme = new()
        {
            PaletteLight = _lightPalette,
            PaletteDark = _darkPalette,
            LayoutProperties = new LayoutProperties()
        };

        // 初始化紫色主题（与浅色共享 PaletteLight，暗色部分用紫色palette）
        _purpleTheme = new()
        {
            PaletteLight = _lightPalette,
            PaletteDark = _purpleDarkPalette,
            LayoutProperties = new LayoutProperties()
        };

        // 初始化任务Badge
        UpdateActiveTaskCount();

        // 每3秒更新Badge数量
        _badgeRefreshTimer = new System.Threading.Timer(async _ =>
        {
            var oldCount = _activeTaskCount;
            UpdateActiveTaskCount();

            if (oldCount != _activeTaskCount)
            {
                await InvokeAsync(StateHasChanged);
            }
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    private void UpdateActiveTaskCount()
    {
        var activeTasks = ProgressService.GetAllActiveProgress()
            .Where(t => t.TaskType != TaskType.FolderMonitor);
        _activeTaskCount = activeTasks.Count();
    }

    private async Task ShowTaskQuickViewAsync()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        await DialogService.ShowAsync<TaskQuickViewDialog>("活动任务", options);
    }

    // 循环切换：深色 → 紫色 → 浅色 → 深色
    private void CycleTheme()
    {
        _currentTheme = _currentTheme switch
        {
            ThemeMode.Dark => ThemeMode.Purple,
            ThemeMode.Purple => ThemeMode.Light,
            ThemeMode.Light => ThemeMode.Dark,
            _ => ThemeMode.Dark
        };
    }

    private void SetTheme(ThemeMode mode) => _currentTheme = mode;

    private string GetThemeModeName() => _currentTheme switch
    {
        ThemeMode.Dark => "深色",
        ThemeMode.Purple => "紫色",
        ThemeMode.Light => "浅色",
        _ => "深色"
    };

    private async Task HandleLogout()
    {
        try
        {
            var response = await HttpClient.PostAsync("/api/auth/logout", null);
            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("已登出", Severity.Info);
                NavigationManager.NavigateTo("/login", true);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"登出失败: {ex.Message}", Severity.Error);
        }
    }

    // 当前主题的图标（表示当前状态）
    public string ThemeModeIcon => _currentTheme switch
    {
        ThemeMode.Dark => Icons.Material.Filled.DarkMode,
        ThemeMode.Purple => Icons.Material.Filled.AutoAwesome,
        ThemeMode.Light => Icons.Material.Filled.WbSunny,
        _ => Icons.Material.Filled.DarkMode
    };

    private string AppBarBackgroundStyle => _currentTheme switch
    {
        ThemeMode.Light => "background: rgba(248, 247, 255, 0.88)",
        ThemeMode.Purple => "background: rgba(22, 22, 38, 0.85)",
        _ => "background: rgba(18, 18, 23, 0.88)"
    };

    private readonly PaletteLight _lightPalette = new()
    {
        Black = "#110e2d",
        Primary = "#7c3aed",
        PrimaryDarken = "#5b21b6",
        PrimaryLighten = "#a78bfa",
        Secondary = "#9333ea",
        Tertiary = "#0891b2",
        Background = "#f8f7ff",
        BackgroundGray = "#f1f0fb",
        Surface = "#ffffff",
        AppbarText = "#4a4869",
        AppbarBackground = "rgba(248, 247, 255, 0.88)",
        DrawerBackground = "#f1f0fb",
        TextPrimary = "#2d2b55",
        TextSecondary = "#6b6899",
        Info = "#2563eb",
        Success = "#059669",
        Warning = "#d97706",
        Error = "#dc2626",
        LinesDefault = "#e2e0f5",
        Divider = "#ebe9f8",
        GrayLight = "#e8e6f8",
        GrayLighter = "#f4f3fc",
        ActionDefault = "#6b6899",
    };

    // 深色主题（中性黑底，紫色点缀）
    private readonly PaletteDark _darkPalette = new()
    {
        Primary = "#8b7fff",
        PrimaryDarken = "#6a5fd8",
        PrimaryLighten = "#a99fff",
        Secondary = "#c084fc",
        Tertiary = "#22d3ee",
        Surface = "#18181e",
        Background = "#121217",
        BackgroundGray = "#0d0d12",
        AppbarText = "#c4c2d4",
        AppbarBackground = "rgba(18, 18, 23, 0.88)",
        DrawerBackground = "#0d0d12",
        ActionDefault = "#6e6d85",
        ActionDisabled = "#9999994d",
        ActionDisabledBackground = "#605f6d4d",
        TextPrimary = "#c4c2d4",
        TextSecondary = "#8a8899",
        TextDisabled = "#ffffff33",
        DrawerIcon = "#7b7a91",
        DrawerText = "#7b7a91",
        GrayLight = "#222228",
        GrayLighter = "#18181e",
        Info = "#4d8eff",
        Success = "#34d072",
        Warning = "#ffba4d",
        Error = "#ff4466",
        LinesDefault = "#2a293a",
        TableLines = "#2a293a",
        Divider = "#232233",
        OverlayLight = "#1c1c2e80",
        Black = "#0a0a14",
    };

    // 紫色主题（饱和紫色底，沉浸感更强）
    private readonly PaletteDark _purpleDarkPalette = new()
    {
        Primary = "#8b7fff",
        PrimaryDarken = "#6a5fd8",
        PrimaryLighten = "#a99fff",
        Secondary = "#c084fc",
        Tertiary = "#22d3ee",
        Surface = "#1c1c2e",
        Background = "#13131f",
        BackgroundGray = "#0f0f1a",
        AppbarText = "#c4c2d4",
        AppbarBackground = "rgba(22, 22, 38, 0.85)",
        DrawerBackground = "#0f0f1a",
        ActionDefault = "#6e6d85",
        ActionDisabled = "#9999994d",
        ActionDisabledBackground = "#605f6d4d",
        TextPrimary = "#c4c2d4",
        TextSecondary = "#8a8899",
        TextDisabled = "#ffffff33",
        DrawerIcon = "#7b7a91",
        DrawerText = "#7b7a91",
        GrayLight = "#252438",
        GrayLighter = "#1c1c2e",
        Info = "#4d8eff",
        Success = "#34d072",
        Warning = "#ffba4d",
        Error = "#ff4466",
        LinesDefault = "#2a293a",
        TableLines = "#2a293a",
        Divider = "#232233",
        OverlayLight = "#1c1c2e80",
        Black = "#0a0a14",
    };

    public void Dispose()
    {
        _badgeRefreshTimer?.Dispose();
    }
}
