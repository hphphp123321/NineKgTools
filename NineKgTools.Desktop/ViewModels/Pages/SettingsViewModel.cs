using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;
using Serilog.Events;

namespace NineKgTools.Desktop.ViewModels.Pages;

public enum SettingsGroup { Appearance, Tasks, Identification, Sources, Files, AI, TagMatching, Search, Log, Database }

public enum ThemeChoice { System, Light, Dark }

public partial class SettingsViewModel : PageViewModelBase
{
    private readonly Config _config;
    private readonly ImageCacheService _imageCache;
    private readonly DesktopPreferences _preferences;
    private bool _suppressSave;
    private CancellationTokenSource? _saveDebounceCts;

    public override string Title => "设置";

    // ========== 选中分组 ==========
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGroupAppearance))]
    [NotifyPropertyChangedFor(nameof(IsGroupTasks))]
    [NotifyPropertyChangedFor(nameof(IsGroupIdentification))]
    [NotifyPropertyChangedFor(nameof(IsGroupSources))]
    [NotifyPropertyChangedFor(nameof(IsGroupFiles))]
    [NotifyPropertyChangedFor(nameof(IsGroupAI))]
    [NotifyPropertyChangedFor(nameof(IsGroupTagMatching))]
    [NotifyPropertyChangedFor(nameof(IsGroupSearch))]
    [NotifyPropertyChangedFor(nameof(IsGroupLog))]
    [NotifyPropertyChangedFor(nameof(IsGroupDatabase))]
    private SettingsGroup _selectedGroup = SettingsGroup.Appearance;

    public bool IsGroupAppearance => SelectedGroup == SettingsGroup.Appearance;
    public bool IsGroupTasks => SelectedGroup == SettingsGroup.Tasks;
    public bool IsGroupIdentification => SelectedGroup == SettingsGroup.Identification;
    public bool IsGroupSources => SelectedGroup == SettingsGroup.Sources;
    public bool IsGroupFiles => SelectedGroup == SettingsGroup.Files;
    public bool IsGroupAI => SelectedGroup == SettingsGroup.AI;
    public bool IsGroupTagMatching => SelectedGroup == SettingsGroup.TagMatching;
    public bool IsGroupSearch => SelectedGroup == SettingsGroup.Search;
    public bool IsGroupLog => SelectedGroup == SettingsGroup.Log;
    public bool IsGroupDatabase => SelectedGroup == SettingsGroup.Database;

    // ========== 外观 ==========
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsThemeSystem))]
    [NotifyPropertyChangedFor(nameof(IsThemeLight))]
    [NotifyPropertyChangedFor(nameof(IsThemeDark))]
    private ThemeChoice _theme = ThemeChoice.System;

    public bool IsThemeSystem => Theme == ThemeChoice.System;
    public bool IsThemeLight => Theme == ThemeChoice.Light;
    public bool IsThemeDark => Theme == ThemeChoice.Dark;

    // ========== 关窗行为 ==========
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloseToTray))]
    [NotifyPropertyChangedFor(nameof(IsCloseExit))]
    private CloseAction _closeAction = CloseAction.MinimizeToTray;

    public bool IsCloseToTray => CloseAction == CloseAction.MinimizeToTray;
    public bool IsCloseExit => CloseAction == CloseAction.Exit;

    // ========== 媒体详情页玻璃背景 ==========
    /// <summary>开启后媒体详情页以该作品封面作为模糊背景（自动暗化保证文字可读）。
    /// 写入 <see cref="DesktopPreferences.SetUseGlassBackground"/> 触发持久化 + 广播让
    /// MediaDetailViewModel 实时切换；不必重启 / 重新加载媒体。</summary>
    [ObservableProperty]
    private bool _useGlassBackground;

    // ========== Shell 集成（仅 Win） ==========
    [ObservableProperty]
    private bool _shellIntegrationRegistered;

    public bool ShellIntegrationSupported => ShellIntegrationService.IsSupported;

    // ========== 任务 ==========
    [ObservableProperty] private int _maxConcurrentIdentificationTasks;
    [ObservableProperty] private int _retryCount;

    // ========== 识别 ==========
    [ObservableProperty] private int _timeoutSeconds;
    [ObservableProperty] private bool _autoAddToDatabase;
    [ObservableProperty] private int _pendingRetentionDays;
    [ObservableProperty] private bool _skipCache;
    [ObservableProperty] private double _minSimilarity;

    // ========== 媒体源（监视文件夹） ==========
    [ObservableProperty] private ObservableCollection<string> _watchFolders = new();

    // ========== 文件 ==========
    [ObservableProperty] private long _filesMinimumSize;
    [ObservableProperty] private bool _filesSkipHidden;
    [ObservableProperty] private bool _filesSkipSystem;

    // ========== AI ==========
    [ObservableProperty] private bool _aiUseAi;
    [ObservableProperty] private bool _aiUseAiForKeywordSplitting;
    [ObservableProperty] private string _aiOpenAiApiKey = "";
    [ObservableProperty] private string _aiOpenAiBaseDomain = "";
    [ObservableProperty] private string _aiOpenAiApiVersion = "";
    [ObservableProperty] private string _aiOpenAiDefaultModel = "";

    // ========== 标签匹配 ==========
    [ObservableProperty] private bool _tmEnableFuzzyMatching;
    [ObservableProperty] private double _tmSimilarityThreshold;
    [ObservableProperty] private bool _tmEnableContainsMatching;
    [ObservableProperty] private bool _tmEnableNormalizedMatching;
    [ObservableProperty] private int _tmMaxMatchResults;

    // ========== 搜索 ==========
    [ObservableProperty] private bool _searchEnableGlobal;
    [ObservableProperty] private bool _searchEnableCache;
    [ObservableProperty] private int _searchCacheExpirationMinutes;
    [ObservableProperty] private int _searchMaxConcurrent;
    [ObservableProperty] private int _searchTimeoutSeconds;
    [ObservableProperty] private double _searchDefaultMinRelevance;

    // ========== 日志 ==========
    [ObservableProperty] private string _logLevelChoice = "Information";

    public ObservableCollection<string> LogLevelOptions { get; } = new()
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    // ========== 数据库（只读） ==========
    [ObservableProperty] private string _databasePath = "";
    [ObservableProperty] private string _hangfirePath = "";
    [ObservableProperty] private string _dataDirectory = "";

    [ObservableProperty] private string? _saveStatusText;

    private readonly ShellIntegrationService _shellIntegration;

    public SettingsViewModel(Config config, ImageCacheService imageCache, DesktopPreferences preferences,
        ShellIntegrationService shellIntegration)
    {
        _config = config;
        _imageCache = imageCache;
        _preferences = preferences;
        _shellIntegration = shellIntegration;
        LoadFromConfig();
    }

    private void LoadFromConfig()
    {
        _suppressSave = true;
        try
        {
            // 任务
            MaxConcurrentIdentificationTasks = _config.Tasks?.MaxConcurrentIdentificationTasks ?? 5;
            RetryCount = _config.Tasks?.RetryCount ?? 3;

            // 识别
            TimeoutSeconds = _config.Identification?.TimeoutSeconds ?? 30;
            AutoAddToDatabase = _config.Identification?.AutoAddToDatabase ?? true;
            PendingRetentionDays = _config.Identification?.PendingRetentionDays ?? 30;
            SkipCache = _config.Identification?.SkipCache ?? false;
            MinSimilarity = _config.Identification?.MinSimilarity ?? 0;

            // 媒体源
            WatchFolders = new ObservableCollection<string>(_config.Source?.WatchFolders ?? new List<string>());

            // 文件
            FilesMinimumSize = _config.Files?.MinimumFileSize ?? 1024;
            FilesSkipHidden = _config.Files?.SkipHiddenFiles ?? true;
            FilesSkipSystem = _config.Files?.SkipSystemFiles ?? true;

            // AI
            AiUseAi = _config.Ai?.UseAi ?? false;
            AiUseAiForKeywordSplitting = _config.Ai?.UseAiForKeywordSplitting ?? false;
            AiOpenAiApiKey = _config.Ai?.OpenAi?.ApiKey ?? "";
            AiOpenAiBaseDomain = _config.Ai?.OpenAi?.BaseDomain ?? "https://api.openai.com";
            AiOpenAiApiVersion = _config.Ai?.OpenAi?.ApiVersion ?? "v1";
            AiOpenAiDefaultModel = _config.Ai?.OpenAi?.DefaultModel ?? "gpt-4o-mini";

            // 标签匹配
            TmEnableFuzzyMatching = _config.TagMatching?.EnableFuzzyMatching ?? true;
            TmSimilarityThreshold = _config.TagMatching?.SimilarityThreshold ?? 0.7;
            TmEnableContainsMatching = _config.TagMatching?.EnableContainsMatching ?? true;
            TmEnableNormalizedMatching = _config.TagMatching?.EnableNormalizedMatching ?? true;
            TmMaxMatchResults = _config.TagMatching?.MaxMatchResults ?? 5;

            // 搜索
            SearchEnableGlobal = _config.Search?.EnableGlobalSearch ?? true;
            SearchEnableCache = _config.Search?.EnableSearchCache ?? true;
            SearchCacheExpirationMinutes = _config.Search?.CacheExpirationMinutes ?? 5;
            SearchMaxConcurrent = _config.Search?.MaxConcurrentSearches ?? 10;
            SearchTimeoutSeconds = _config.Search?.SearchTimeoutSeconds ?? 30;
            SearchDefaultMinRelevance = _config.Search?.DefaultMinRelevanceScore ?? 0.3;

            // 日志
            LogLevelChoice = (_config.Log?.LogLevel ?? LogEventLevel.Information).ToString();

            // 数据库
            DatabasePath = _config.Database?.Path ?? "";
            HangfirePath = _config.Database?.HangfirePath ?? "";
            DataDirectory = Environment.CurrentDirectory;

            // 主题：从 DesktopPreferences 持久化字段读，回退到当前 Application 主题
            if (Enum.TryParse<ThemeChoice>(_preferences.Theme, ignoreCase: true, out var saved))
            {
                Theme = saved;
            }
            else
            {
                var current = Application.Current?.RequestedThemeVariant;
                if (current == ThemeVariant.Light) Theme = ThemeChoice.Light;
                else if (current == ThemeVariant.Dark) Theme = ThemeChoice.Dark;
                else Theme = ThemeChoice.System;
            }

            // 关窗行为：从 DesktopPreferences 读
            CloseAction = _preferences.CloseAction;
            UseGlassBackground = _preferences.UseGlassBackground;

            // Shell 集成状态：以 ShellIntegrationService 实际检测为准（注册表可能被外部修改）
            ShellIntegrationRegistered = _shellIntegration.IsRegistered();
            _preferences.ShellIntegrationRegistered = ShellIntegrationRegistered;
        }
        finally
        {
            _suppressSave = false;
        }
    }

    [RelayCommand]
    private void SelectGroup(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName)) return;
        if (Enum.TryParse<SettingsGroup>(groupName, ignoreCase: true, out var g))
            SelectedGroup = g;
    }

    [RelayCommand]
    private void SelectTheme(string? themeName)
    {
        if (string.IsNullOrEmpty(themeName)) return;
        if (!Enum.TryParse<ThemeChoice>(themeName, ignoreCase: true, out var t)) return;
        Theme = t;
        ApplyTheme();
        if (!_suppressSave)
        {
            _preferences.Theme = t.ToString();
            _preferences.RequestSave();
        }
    }

    [RelayCommand]
    private void SelectCloseAction(string? actionName)
    {
        if (string.IsNullOrEmpty(actionName)) return;
        if (!Enum.TryParse<CloseAction>(actionName, ignoreCase: true, out var a)) return;
        CloseAction = a;
        if (!_suppressSave)
        {
            _preferences.CloseAction = a;
            _preferences.RequestSave();
        }
    }

    /// <summary>测试 Shell verb——通过 IPC 自连发送 show-main，验证现有进程能收到 verb 调用</summary>
    [RelayCommand]
    private async Task TestShellIntegrationAsync()
    {
        if (!ShellIntegrationSupported)
        {
            SaveStatusText = "当前平台不支持 Shell 集成";
            return;
        }
        if (!ShellIntegrationRegistered)
        {
            SaveStatusText = "请先启用 Shell 集成再测试";
            return;
        }

        try
        {
            // 自连——发个 show-main 到 IPC 服务器，往返成功即说明命令通道工作
            var ok = await IpcService.TrySendAsync(new IpcCommand { Cmd = "show-main" });
            SaveStatusText = ok
                ? "测试成功 · IPC 通道已就绪，右键菜单调用会被本进程接住"
                : "测试失败 · 现有进程无响应（IpcService server 可能未启动）";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestShellIntegration 失败");
            SaveStatusText = "测试失败，请稍后重试";
        }
    }

    /// <summary>手动重置注册——先 Unregister 再 Register。注册表残留 / 路径漂移时用。</summary>
    [RelayCommand]
    private async Task ResetShellRegistrationAsync()
    {
        if (!ShellIntegrationSupported) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "重置 Shell 集成注册",
            message: "将清除当前用户注册表的 NineKgTools 右键 verb，然后用当前 exe 路径重新注册。如果应用被移动过位置或注册表被外部修改，这能修复。",
            intent: DialogIntent.Affirmative,
            confirmText: "确认重置");
        if (!confirmed) return;

        try
        {
            _shellIntegration.Unregister();
            var ok = _shellIntegration.Register();
            ShellIntegrationRegistered = _shellIntegration.IsRegistered();
            _preferences.ShellIntegrationRegistered = ShellIntegrationRegistered;
            _preferences.RequestSave();
            SaveStatusText = ok ? "重置完成 · 已用当前 exe 路径重新注册" : "重置失败，请稍后重试";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ResetShellRegistration 失败");
            SaveStatusText = "重置失败，请稍后重试";
        }
    }

    /// <summary>切换 Shell 集成（注册 / 卸载 HKCU verb）</summary>
    [RelayCommand]
    private void ToggleShellIntegration()
    {
        if (!ShellIntegrationSupported) return;

        bool ok;
        if (ShellIntegrationRegistered)
        {
            ok = _shellIntegration.Unregister();
            if (ok)
            {
                ShellIntegrationRegistered = false;
                _preferences.ShellIntegrationRegistered = false;
                _preferences.RequestSave();
                SaveStatusText = "Shell 集成已卸载";
            }
            else
            {
                SaveStatusText = "卸载失败，请稍后重试";
            }
        }
        else
        {
            ok = _shellIntegration.Register();
            if (ok)
            {
                ShellIntegrationRegistered = true;
                _preferences.ShellIntegrationRegistered = true;
                _preferences.RequestSave();
                SaveStatusText = "Shell 集成已注册（Win11 需「显示更多选项」）";
            }
            else
            {
                SaveStatusText = "注册失败，请稍后重试";
            }
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current is null) return;
        Application.Current.RequestedThemeVariant = Theme switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }

    // ============================================================
    //  字段变更 handlers
    // ============================================================

    /// <summary>用户 toggle "详情页封面背景"——写回 DesktopPreferences 并广播让
    /// MediaDetailViewModel 实时切换 UI（不必重启 / 重新加载媒体）</summary>
    partial void OnUseGlassBackgroundChanged(bool value)
    {
        if (_suppressSave) return;
        _preferences.SetUseGlassBackground(value);
    }

    partial void OnMaxConcurrentIdentificationTasksChanged(int value)
    {
        if (_suppressSave || _config.Tasks is null) return;
        _config.Tasks.MaxConcurrentIdentificationTasks = Math.Max(1, value);
        DebouncedSave();
    }

    partial void OnRetryCountChanged(int value)
    {
        if (_suppressSave || _config.Tasks is null) return;
        _config.Tasks.RetryCount = Math.Max(0, value);
        DebouncedSave();
    }

    partial void OnTimeoutSecondsChanged(int value)
    {
        if (_suppressSave || _config.Identification is null) return;
        _config.Identification.TimeoutSeconds = Math.Max(5, value);
        DebouncedSave();
    }

    partial void OnAutoAddToDatabaseChanged(bool value)
    {
        if (_suppressSave || _config.Identification is null) return;
        _config.Identification.AutoAddToDatabase = value;
        DebouncedSave();
    }

    partial void OnPendingRetentionDaysChanged(int value)
    {
        if (_suppressSave || _config.Identification is null) return;
        _config.Identification.PendingRetentionDays = Math.Max(0, value);
        DebouncedSave();
    }

    partial void OnSkipCacheChanged(bool value)
    {
        if (_suppressSave || _config.Identification is null) return;
        _config.Identification.SkipCache = value;
        DebouncedSave();
    }

    partial void OnMinSimilarityChanged(double value)
    {
        if (_suppressSave || _config.Identification is null) return;
        _config.Identification.MinSimilarity = Math.Clamp(value, 0, 1);
        DebouncedSave();
    }

    partial void OnFilesMinimumSizeChanged(long value)
    {
        if (_suppressSave || _config.Files is null) return;
        _config.Files.MinimumFileSize = Math.Max(0, value);
        DebouncedSave();
    }

    partial void OnFilesSkipHiddenChanged(bool value)
    {
        if (_suppressSave || _config.Files is null) return;
        _config.Files.SkipHiddenFiles = value;
        DebouncedSave();
    }

    partial void OnFilesSkipSystemChanged(bool value)
    {
        if (_suppressSave || _config.Files is null) return;
        _config.Files.SkipSystemFiles = value;
        DebouncedSave();
    }

    partial void OnAiUseAiChanged(bool value)
    {
        if (_suppressSave || _config.Ai is null) return;
        _config.Ai.UseAi = value;
        DebouncedSave();
    }

    partial void OnAiUseAiForKeywordSplittingChanged(bool value)
    {
        if (_suppressSave || _config.Ai is null) return;
        _config.Ai.UseAiForKeywordSplitting = value;
        DebouncedSave();
    }

    partial void OnAiOpenAiApiKeyChanged(string value)
    {
        if (_suppressSave || _config.Ai?.OpenAi is null) return;
        _config.Ai.OpenAi.ApiKey = value;
        DebouncedSave();
    }

    partial void OnAiOpenAiBaseDomainChanged(string value)
    {
        if (_suppressSave || _config.Ai?.OpenAi is null) return;
        _config.Ai.OpenAi.BaseDomain = value;
        DebouncedSave();
    }

    partial void OnAiOpenAiApiVersionChanged(string value)
    {
        if (_suppressSave || _config.Ai?.OpenAi is null) return;
        _config.Ai.OpenAi.ApiVersion = value;
        DebouncedSave();
    }

    partial void OnAiOpenAiDefaultModelChanged(string value)
    {
        if (_suppressSave || _config.Ai?.OpenAi is null) return;
        _config.Ai.OpenAi.DefaultModel = value;
        DebouncedSave();
    }

    partial void OnTmEnableFuzzyMatchingChanged(bool value)
    {
        if (_suppressSave || _config.TagMatching is null) return;
        _config.TagMatching.EnableFuzzyMatching = value;
        DebouncedSave();
    }

    partial void OnTmSimilarityThresholdChanged(double value)
    {
        if (_suppressSave || _config.TagMatching is null) return;
        _config.TagMatching.SimilarityThreshold = Math.Clamp(value, 0, 1);
        DebouncedSave();
    }

    partial void OnTmEnableContainsMatchingChanged(bool value)
    {
        if (_suppressSave || _config.TagMatching is null) return;
        _config.TagMatching.EnableContainsMatching = value;
        DebouncedSave();
    }

    partial void OnTmEnableNormalizedMatchingChanged(bool value)
    {
        if (_suppressSave || _config.TagMatching is null) return;
        _config.TagMatching.EnableNormalizedMatching = value;
        DebouncedSave();
    }

    partial void OnTmMaxMatchResultsChanged(int value)
    {
        if (_suppressSave || _config.TagMatching is null) return;
        _config.TagMatching.MaxMatchResults = Math.Max(1, value);
        DebouncedSave();
    }

    partial void OnSearchEnableGlobalChanged(bool value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.EnableGlobalSearch = value;
        DebouncedSave();
    }

    partial void OnSearchEnableCacheChanged(bool value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.EnableSearchCache = value;
        DebouncedSave();
    }

    partial void OnSearchCacheExpirationMinutesChanged(int value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.CacheExpirationMinutes = Math.Max(1, value);
        DebouncedSave();
    }

    partial void OnSearchMaxConcurrentChanged(int value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.MaxConcurrentSearches = Math.Max(1, value);
        DebouncedSave();
    }

    partial void OnSearchTimeoutSecondsChanged(int value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.SearchTimeoutSeconds = Math.Max(1, value);
        DebouncedSave();
    }

    partial void OnSearchDefaultMinRelevanceChanged(double value)
    {
        if (_suppressSave || _config.Search is null) return;
        _config.Search.DefaultMinRelevanceScore = Math.Clamp(value, 0, 1);
        DebouncedSave();
    }

    partial void OnLogLevelChoiceChanged(string value)
    {
        if (_suppressSave || _config.Log is null) return;
        if (Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var lv))
        {
            _config.Log.LogLevel = lv;
            DebouncedSave();
        }
    }

    // ============================================================
    //  媒体源（监视文件夹）—— 列表编辑
    // ============================================================

    [RelayCommand]
    private async Task AddWatchFolderAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            if (topLevel?.StorageProvider is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择要监视的文件夹",
                AllowMultiple = false,
            });
            if (folders.Count == 0) return;
            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            // 防止重复添加
            if (WatchFolders.Contains(path)) return;

            WatchFolders.Add(path);
            PersistWatchFolders();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加监视文件夹失败");
            SaveStatusText = "添加监视文件夹失败";
        }
    }

    [RelayCommand]
    private void RemoveWatchFolder(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!WatchFolders.Contains(path)) return;
        WatchFolders.Remove(path);
        PersistWatchFolders();
    }

    private void PersistWatchFolders()
    {
        if (_config.Source is null) return;
        _config.Source.WatchFolders = WatchFolders.ToList();
        DebouncedSave();
    }

    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is not null)
        {
            return Avalonia.Controls.TopLevel.GetTopLevel(lifetime.MainWindow);
        }
        return null;
    }

    /// <summary>500ms 防抖保存到 config.yaml</summary>
    private void DebouncedSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        await _config.SaveConfig();
                        SaveStatusText = $"已保存 · {DateTime.Now:HH:mm:ss}";
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Settings 保存失败");
                        SaveStatusText = "保存失败";
                    }
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    // ========== 危险操作 ==========

    [RelayCommand]
    private void OpenDataDirectory()
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", DataDirectory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开数据目录失败");
        }
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "清空缓存",
            message: "将清除内存中的图片 LRU 缓存以及磁盘缓存目录。下次访问媒体时会重新加载封面图，可能略慢。",
            intent: DialogIntent.Affirmative,
            confirmText: "立即清空");
        if (!confirmed) return;

        try
        {
            _imageCache.Clear();
            var cacheDir = Path.Combine(DataDirectory, ".cache");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
                Directory.CreateDirectory(cacheDir);
            }
            SaveStatusText = "缓存已清空";
            Log.Information("Settings 触发清空缓存");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "清空缓存失败");
            SaveStatusText = "清空缓存失败";
        }
    }

    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "重置为默认",
            message: "将把 config.yaml 全部字段恢复为初始 example 模板值。已配置的监视文件夹、网站 ApiKey、AI 设置等都会丢失。**数据库与媒体文件不受影响**。",
            intent: DialogIntent.Destructive,
            confirmText: "确认重置");
        if (!confirmed) return;

        try
        {
            var configDir = Path.Combine(DataDirectory, "Config");
            var configPath = Path.Combine(configDir, "config.yaml");
            var examplePath = Path.Combine(configDir, "config.example.yaml");

            if (!File.Exists(examplePath))
            {
                Log.Warning("config.example.yaml 不存在，无法重置");
                SaveStatusText = "重置失败：找不到 example 模板";
                return;
            }

            var backupPath = Path.Combine(configDir, $"config.backup.{DateTime.Now:yyyyMMddHHmmss}.yaml");
            File.Copy(configPath, backupPath, overwrite: true);
            File.Copy(examplePath, configPath, overwrite: true);

            await _config.InitConfig();
            LoadFromConfig();

            SaveStatusText = $"已重置 · 旧配置备份至 config.backup.{DateTime.Now:yyyyMMddHHmmss}.yaml";
            Log.Information("Settings 触发重置默认，旧配置备份至 {Path}", backupPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重置默认失败");
            SaveStatusText = "重置失败，请稍后重试";
        }
    }
}
