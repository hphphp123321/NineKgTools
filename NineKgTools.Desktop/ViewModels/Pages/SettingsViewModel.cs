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

public enum SettingsGroup { Appearance, Tasks, Identification, Files, AI, TagMatching, Search, Log, Application }

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
    [NotifyPropertyChangedFor(nameof(IsGroupFiles))]
    [NotifyPropertyChangedFor(nameof(IsGroupAI))]
    [NotifyPropertyChangedFor(nameof(IsGroupTagMatching))]
    [NotifyPropertyChangedFor(nameof(IsGroupSearch))]
    [NotifyPropertyChangedFor(nameof(IsGroupLog))]
    [NotifyPropertyChangedFor(nameof(IsGroupApplication))]
    private SettingsGroup _selectedGroup = SettingsGroup.Appearance;

    public bool IsGroupAppearance => SelectedGroup == SettingsGroup.Appearance;
    public bool IsGroupTasks => SelectedGroup == SettingsGroup.Tasks;
    public bool IsGroupIdentification => SelectedGroup == SettingsGroup.Identification;
    public bool IsGroupFiles => SelectedGroup == SettingsGroup.Files;
    public bool IsGroupAI => SelectedGroup == SettingsGroup.AI;
    public bool IsGroupTagMatching => SelectedGroup == SettingsGroup.TagMatching;
    public bool IsGroupSearch => SelectedGroup == SettingsGroup.Search;
    public bool IsGroupLog => SelectedGroup == SettingsGroup.Log;
    public bool IsGroupApplication => SelectedGroup == SettingsGroup.Application;

    // ========== 通用 - 主题 ==========
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

    // ========== 开机自启（仅 Win） ==========
    [ObservableProperty]
    private bool _autoStartEnabled;

    public bool AutoStartSupported => AutoStartService.IsSupported;

    // ========== 应用 / 更新 ==========
    /// <summary>当前版本号（Velopack 安装版取其版本，否则回退程序集版本）。</summary>
    public string CurrentVersionText => _updateService.CurrentVersionText;

    /// <summary>是否 Velopack 安装版——dev / portable 下为 false，"检查更新"提示不可用。</summary>
    public bool UpdateSupported => _updateService.IsSupported;

    /// <summary>启动时静默检查更新（镜像 <see cref="DesktopPreferences.AutoCheckUpdates"/>）。</summary>
    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    /// <summary>"立即检查更新"按钮旁的状态文案。</summary>
    [ObservableProperty]
    private string _updateCheckStatus = "";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    // ========== 任务 ==========
    [ObservableProperty] private int _maxConcurrentIdentificationTasks;
    [ObservableProperty] private int _retryCount;

    // ========== 识别 ==========
    [ObservableProperty] private int _timeoutSeconds;
    [ObservableProperty] private bool _autoAddToDatabase;
    [ObservableProperty] private int _pendingRetentionDays;
    [ObservableProperty] private bool _skipCache;
    [ObservableProperty] private double _minSimilarity;

    // ========== 文件 ==========
    [ObservableProperty] private long _filesMinimumSize;
    [ObservableProperty] private bool _filesSkipHidden;
    [ObservableProperty] private bool _filesSkipSystem;

    // 高级过滤规则三组 chip 编辑器（忽略文件名 / 忽略模式 / 允许扩展名）。
    // 默认值与 FilesConfig 字段初值保持一致——改 FilesConfig 默认时这里同步。
    public StringListEditorViewModel IgnoredFilesEditor { get; }
    public StringListEditorViewModel IgnoredPatternsEditor { get; }
    public StringListEditorViewModel AllowedExtensionsEditor { get; }

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

    /// <summary>数据根目录——不暴露到 UI，仅 OpenDataDirectory / ClearCache / ResetDefaults 内部用。</summary>
    private string _dataDirectory = "";

    [ObservableProperty] private string? _saveStatusText;

    private readonly ShellIntegrationService _shellIntegration;
    private readonly AutoStartService _autoStart;
    private readonly UpdateService _updateService;

    public SettingsViewModel(Config config, ImageCacheService imageCache, DesktopPreferences preferences,
        ShellIntegrationService shellIntegration, AutoStartService autoStart, UpdateService updateService)
    {
        _config = config;
        _imageCache = imageCache;
        _preferences = preferences;
        _shellIntegration = shellIntegration;
        _autoStart = autoStart;
        _updateService = updateService;

        IgnoredFilesEditor = new StringListEditorViewModel(
            placeholder: "如 Thumbs.db（精确文件名，回车添加）",
            emptyHint: "留空 = 不按文件名忽略",
            defaults: new[] { "Thumbs.db", ".DS_Store", "desktop.ini", ".gitkeep", ".gitignore" });
        IgnoredPatternsEditor = new StringListEditorViewModel(
            placeholder: "如 *.tmp（支持 * 通配符，回车添加）",
            emptyHint: "留空 = 不按模式忽略",
            defaults: new[] { ".*", "~*", "*.tmp", "*.temp", "*.cache", "*.log", "*.bak", "*.swp" });
        AllowedExtensionsEditor = new StringListEditorViewModel(
            placeholder: "如 mp4（回车添加，自动补 .）",
            emptyHint: "留空 = 允许所有扩展名",
            defaults: Array.Empty<string>(),
            normalizer: NormalizeExtension);

        // 三组任一增删 → 写回 config.Files + 防抖落盘（_suppressSave 期由 SetItems 触发的不落盘）
        IgnoredFilesEditor.Items.CollectionChanged += (_, _) => OnFilesAdvancedListChanged();
        IgnoredPatternsEditor.Items.CollectionChanged += (_, _) => OnFilesAdvancedListChanged();
        AllowedExtensionsEditor.Items.CollectionChanged += (_, _) => OnFilesAdvancedListChanged();

        LoadFromConfig();
    }

    /// <summary>扩展名规范化：去空白 + 小写 + 补 "." 前缀（mp4 → .mp4）。</summary>
    private static string NormalizeExtension(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return "";
        if (!v.StartsWith('.')) v = "." + v;
        return v;
    }

    /// <summary>高级过滤三组任一变化 → 同步回 config.Files + 防抖保存。扫描时实时读取，无需重启。</summary>
    private void OnFilesAdvancedListChanged()
    {
        if (_suppressSave || _config.Files is null) return;
        _config.Files.IgnoredFiles = IgnoredFilesEditor.Items.ToList();
        _config.Files.IgnoredPatterns = IgnoredPatternsEditor.Items.ToList();
        _config.Files.AllowedExtensions = AllowedExtensionsEditor.Items.ToList();
        DebouncedSave();
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

            // 文件
            FilesMinimumSize = _config.Files?.MinimumFileSize ?? 1024;
            FilesSkipHidden = _config.Files?.SkipHiddenFiles ?? true;
            FilesSkipSystem = _config.Files?.SkipSystemFiles ?? true;
            // 高级过滤三组（_suppressSave=true 期填充，CollectionChanged 不会触发落盘）
            IgnoredFilesEditor.SetItems(_config.Files?.IgnoredFiles);
            IgnoredPatternsEditor.SetItems(_config.Files?.IgnoredPatterns);
            AllowedExtensionsEditor.SetItems(_config.Files?.AllowedExtensions);

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

            // 数据根目录（内部用，不暴露 UI）
            _dataDirectory = Environment.CurrentDirectory;

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

            // 开机自启状态：以 AutoStartService 实际检测为准（HKCU Run 可能被外部修改）
            AutoStartEnabled = _autoStart.IsEnabled();
            _preferences.AutoStartEnabled = AutoStartEnabled;

            // 应用 / 更新
            AutoCheckUpdates = _preferences.AutoCheckUpdates;
            OnPropertyChanged(nameof(CurrentVersionText));
            OnPropertyChanged(nameof(UpdateSupported));
            UpdateCheckStatus = _preferences.LastUpdateCheck is { } lastCheck
                ? $"上次检查：{lastCheck.ToLocalTime():yyyy-MM-dd HH:mm}"
                : "";
        }
        finally
        {
            _suppressSave = false;
        }
    }

    /// <summary>"启动时自动检查更新"开关 → 镜像到 DesktopPreferences 并落盘。</summary>
    partial void OnAutoCheckUpdatesChanged(bool value)
    {
        if (_suppressSave) return;
        _preferences.AutoCheckUpdates = value;
        _preferences.RequestSave();
    }

    /// <summary>"立即检查更新"：仅 Velopack 安装版有效；有新版直接走下载+应用流程，否则提示已最新。</summary>
    [RelayCommand]
    private async Task CheckUpdatesNowAsync()
    {
        if (IsCheckingUpdate) return;
        if (!_updateService.IsSupported)
        {
            UpdateCheckStatus = "当前为开发 / 便携版，自动更新不可用。";
            return;
        }

        IsCheckingUpdate = true;
        UpdateCheckStatus = "正在检查…";
        try
        {
            var info = await _updateService.CheckAsync();
            _preferences.LastUpdateCheck = DateTime.UtcNow;
            _preferences.RequestSave();

            if (info?.TargetFullRelease is { } rel)
            {
                var version = rel.Version?.ToString() ?? "";
                UpdateCheckStatus = $"发现新版本 {version}";
                var ok = await NineKgConfirmDialog.ShowAsync(null,
                    title: "发现新版本",
                    message: $"版本 {version} 可用，是否立即下载并更新？更新完成后应用会自动重启。",
                    intent: DialogIntent.Affirmative,
                    confirmText: "立即更新");
                if (ok)
                    await Views.Dialogs.UpdateProgressDialog.RunAsync(null, _updateService, info);
            }
            else
            {
                UpdateCheckStatus = $"已是最新版本（{CurrentVersionText}）";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Settings 检查更新失败");
            UpdateCheckStatus = "检查失败，请稍后重试。";
        }
        finally
        {
            IsCheckingUpdate = false;
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

    /// <summary>切换开机自启（注册 / 卸载 HKCU Run 值）</summary>
    [RelayCommand]
    private void ToggleAutoStart()
    {
        if (!AutoStartSupported) return;

        bool ok;
        if (AutoStartEnabled)
        {
            ok = _autoStart.Disable();
            if (ok)
            {
                AutoStartEnabled = false;
                _preferences.AutoStartEnabled = false;
                _preferences.RequestSave();
                SaveStatusText = "开机自启已关闭";
            }
            else
            {
                SaveStatusText = "操作失败，请稍后重试。";
            }
        }
        else
        {
            ok = _autoStart.Enable();
            if (ok)
            {
                AutoStartEnabled = true;
                _preferences.AutoStartEnabled = true;
                _preferences.RequestSave();
                SaveStatusText = "开机自启已开启 · 登录时静默到托盘启动";
            }
            else
            {
                SaveStatusText = "操作失败，请稍后重试。";
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
            System.Diagnostics.Process.Start("explorer.exe", _dataDirectory);
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
            var cacheDir = Path.Combine(_dataDirectory, ".cache");
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
            var configDir = Path.Combine(_dataDirectory, "Config");
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
