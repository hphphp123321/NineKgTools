using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Websites;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 识别网站配置页。Phase 2.2：三张站点配置卡 + 识别优先级 6 行卡（与 Web 对齐：
/// 全部分类一屏展示 / chip 可单独删除 / + 添加 走 Flyout，按"已添加""不兼容"过滤）。
/// </summary>
public partial class WebsitesViewModel : PageViewModelBase
{
    private readonly Config _config;
    private readonly WebsiteService _websiteService;
    private bool _suppressSave;
    private CancellationTokenSource? _saveDebounceCts;

    public override string Title => "网站";

    // ========== DLsite ==========
    [ObservableProperty]
    private bool _dLsiteEnable;

    [ObservableProperty]
    private bool _dLsiteUseSelenium;

    // ========== Bangumi ==========
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BangumiApiKeyMasked))]
    [NotifyPropertyChangedFor(nameof(HasBangumiApiKey))]
    private bool _bangumiEnable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BangumiApiKeyMasked))]
    [NotifyPropertyChangedFor(nameof(HasBangumiApiKey))]
    private string _bangumiApiKey = "";

    public bool HasBangumiApiKey => !string.IsNullOrWhiteSpace(BangumiApiKey);
    public string BangumiApiKeyMasked => HasBangumiApiKey
        ? $"已配置 · 末 4 位 …{BangumiApiKey[^Math.Min(4, BangumiApiKey.Length)..]}"
        : "未配置 ApiKey";

    // ========== Steam ==========
    [ObservableProperty]
    private bool _steamEnable;

    [ObservableProperty]
    private string _steamLanguage = "schinese";

    [ObservableProperty]
    private string _steamCountryCode = "us";

    public ObservableCollection<SteamLanguageOption> SteamLanguages { get; } = new()
    {
        new("schinese", "简体中文"),
        new("english", "英文"),
        new("japanese", "日文"),
        new("tchinese", "繁体中文"),
    };

    /// <summary>
    /// 不包含 cn——部分游戏对 CN 区屏蔽，会导致 success=false。UI 用警示文案替代禁用项。
    /// </summary>
    public ObservableCollection<SteamCountryOption> SteamCountries { get; } = new()
    {
        new("us", "美国 (US)"),
        new("jp", "日本 (JP)"),
        new("hk", "香港 (HK)"),
        new("tw", "台湾 (TW)"),
        new("uk", "英国 (UK)"),
        new("de", "德国 (DE)"),
    };

    [ObservableProperty]
    private string? _saveStatusText;

    // ========== 网站优先级（6 行全开版，与 Web 对齐） ==========

    /// <summary>6 行分类（视频/音频/游戏/图片/文字/未知）。每行独立持 Chips 集合，CollectionChanged 自动回写 Config + 防抖保存。</summary>
    public IReadOnlyList<PriorityRowViewModel> PriorityRows { get; }

    public WebsitesViewModel(Config config, WebsiteService websiteService)
    {
        _config = config;
        _websiteService = websiteService;
        LoadFromConfig();
        PriorityRows = BuildPriorityRows();
    }

    private void LoadFromConfig()
    {
        _suppressSave = true;
        try
        {
            var w = _config.Website;
            if (w?.DLsite is not null)
            {
                DLsiteEnable = w.DLsite.Enable;
                DLsiteUseSelenium = w.DLsite.UseSeleniumForRating;
            }
            if (w?.Bangumi is not null)
            {
                BangumiEnable = w.Bangumi.Enable;
                BangumiApiKey = w.Bangumi.ApiKey ?? "";
            }
            if (w?.Steam is not null)
            {
                SteamEnable = w.Steam.Enable;
                SteamLanguage = string.IsNullOrWhiteSpace(w.Steam.Language) ? "schinese" : w.Steam.Language;
                SteamCountryCode = string.Equals(w.Steam.CountryCode, "cn", StringComparison.OrdinalIgnoreCase)
                    ? "us"
                    : (string.IsNullOrWhiteSpace(w.Steam.CountryCode) ? "us" : w.Steam.CountryCode);
            }
        }
        finally
        {
            _suppressSave = false;
        }
    }

    private IReadOnlyList<PriorityRowViewModel> BuildPriorityRows()
    {
        var priority = _config.Website?.Priority;
        var map = _websiteService.WebsiteNameMap;

        // 顺序按 Web 端 CategoryZones 一致：视频/音频/图片/文字/游戏/未知
        return new[]
        {
            CreateRow("Video", "视频", "IconCategoryVideo", "BrandCategoryVideoBrush", "BrandCategoryVideoFillBrush", TopCategory.Video, priority?.Video, map),
            CreateRow("Audio", "音频", "IconCategoryAudio", "BrandCategoryAudioBrush", "BrandCategoryAudioFillBrush", TopCategory.Audio, priority?.Audio, map),
            CreateRow("Picture", "图片", "IconCategoryPicture", "BrandCategoryPictureBrush", "BrandCategoryPictureFillBrush", TopCategory.Picture, priority?.Picture, map),
            CreateRow("Text", "文字", "IconCategoryText", "BrandCategoryTextBrush", "BrandCategoryTextFillBrush", TopCategory.Text, priority?.Text, map),
            CreateRow("Game", "游戏", "IconCategoryGame", "BrandCategoryGameBrush", "BrandCategoryGameFillBrush", TopCategory.Game, priority?.Game, map),
            CreateRow("Unknown", "未知", "IconCategoryUnknown", "BrandCategoryAllBrush", "BrandCategoryAllFillBrush", TopCategory.Unknown, priority?.Unknown, map),
        };
    }

    private PriorityRowViewModel CreateRow(
        string key, string displayName, string iconKey, string accentKey, string fillKey,
        TopCategory category, List<string>? configList, IDictionary<string, IWebsite> map)
    {
        configList ??= new List<string>();
        return new PriorityRowViewModel(key, displayName, iconKey, accentKey, fillKey, category, configList, map, OnPriorityDirty);
    }

    private void OnPriorityDirty() => DebouncedSave();

    partial void OnDLsiteEnableChanged(bool value)
    {
        if (_suppressSave || _config.Website?.DLsite is null) return;
        _config.Website.DLsite.Enable = value;
        DebouncedSave();
    }

    partial void OnDLsiteUseSeleniumChanged(bool value)
    {
        if (_suppressSave || _config.Website?.DLsite is null) return;
        _config.Website.DLsite.UseSeleniumForRating = value;
        DebouncedSave();
    }

    partial void OnBangumiEnableChanged(bool value)
    {
        if (_suppressSave || _config.Website?.Bangumi is null) return;
        _config.Website.Bangumi.Enable = value;
        DebouncedSave();
    }

    partial void OnBangumiApiKeyChanged(string value)
    {
        if (_suppressSave || _config.Website?.Bangumi is null) return;
        _config.Website.Bangumi.ApiKey = value ?? "";
        DebouncedSave();
    }

    partial void OnSteamEnableChanged(bool value)
    {
        if (_suppressSave || _config.Website?.Steam is null) return;
        _config.Website.Steam.Enable = value;
        DebouncedSave();
    }

    partial void OnSteamLanguageChanged(string value)
    {
        if (_suppressSave || _config.Website?.Steam is null) return;
        _config.Website.Steam.Language = value;
        DebouncedSave();
    }

    partial void OnSteamCountryCodeChanged(string value)
    {
        if (_suppressSave || _config.Website?.Steam is null) return;
        if (string.Equals(value, "cn", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Steam CountryCode 不允许 cn，已重置为 us");
            SteamCountryCode = "us";
            return;
        }
        _config.Website.Steam.CountryCode = value;
        DebouncedSave();
    }

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
                        Log.Error(ex, "Websites 保存失败");
                        SaveStatusText = "保存失败";
                    }
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    [RelayCommand]
    private void OpenBangumiApiKeyPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://next.bgm.tv/demo/access-token",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开 Bangumi 申请页失败");
        }
    }
}

/// <summary>
/// 优先级单行 VM——承载某个分类的 chip 列表 + "+ 添加"可选项。Chips.CollectionChanged 触发回写 Config。
/// 不持 brush 引用：chip 颜色由 Row 持 AccentBrush/FillBrush（一次性 lookup），chip VM 通过 Row 引用拿色，
/// theme 切换需要重新进入页面才能换色（开发阶段可接受，与 frontend-design 同等权衡）。
/// </summary>
public partial class PriorityRowViewModel : ObservableObject
{
    private readonly List<string> _configList;
    private readonly IDictionary<string, IWebsite> _websiteMap;
    private readonly Action _onDirty;
    private bool _suppressWriteback;

    public string Key { get; }
    public string DisplayName { get; }
    public TopCategory Category { get; }
    public IBrush? AccentBrush { get; }
    public IBrush? FillBrush { get; }
    public Geometry? IconGeometry { get; }

    /// <summary>当前行的 chip 列表，按顺序对应 PriorityConfig.{Video|Audio|...}</summary>
    public ObservableCollection<PriorityChipViewModel> Chips { get; } = new();

    /// <summary>Flyout 上段——本分类还能添加的站点。点击触发 AddSiteCommand。</summary>
    public ObservableCollection<AddableSiteOption> AddableSites { get; } = new();

    /// <summary>Flyout 下段——已添加 / 不兼容的站点。仅显示状态，不可点。</summary>
    public ObservableCollection<AddableSiteOption> OtherSites { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyHint))]
    [NotifyPropertyChangedFor(nameof(HasChips))]
    private bool _isEmpty = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyFlyoutMessage))]
    [NotifyPropertyChangedFor(nameof(EmptyFlyoutIsWarning))]
    private bool _hasAddable;

    [ObservableProperty]
    private bool _hasOthers;

    public bool HasChips => !IsEmpty;
    public string EmptyHint => "（尚未配置识别源）";

    /// <summary>Flyout header 文案，按分类名拼。</summary>
    public string FlyoutHeader => $"添加识别源到「{DisplayName}」";

    /// <summary>无可添加时的 empty state 文案。如果其他全是"已添加"→ 全部已加；否则 → 无可用</summary>
    public string EmptyFlyoutMessage
    {
        get
        {
            if (HasAddable) return "";
            var allAdded = OtherSites.All(s => string.Equals(s.UnavailableReason, "已添加", StringComparison.Ordinal));
            return allAdded
                ? $"已添加全部支持「{DisplayName}」的识别源"
                : $"暂无可用于「{DisplayName}」识别的站点";
        }
    }

    public bool EmptyFlyoutIsWarning => !HasAddable && OtherSites.Any(s => !string.Equals(s.UnavailableReason, "已添加", StringComparison.Ordinal));

    public PriorityRowViewModel(
        string key, string displayName, string iconKey, string accentKey, string fillKey,
        TopCategory category, List<string> configList, IDictionary<string, IWebsite> websiteMap,
        Action onDirty)
    {
        Key = key;
        DisplayName = displayName;
        Category = category;
        _configList = configList;
        _websiteMap = websiteMap;
        _onDirty = onDirty;

        AccentBrush = ResolveBrush(accentKey);
        FillBrush = ResolveBrush(fillKey);
        IconGeometry = ResolveGeometry(iconKey);

        _suppressWriteback = true;
        try
        {
            foreach (var siteName in configList)
            {
                Chips.Add(new PriorityChipViewModel(this, siteName));
            }
        }
        finally { _suppressWriteback = false; }

        IsEmpty = Chips.Count == 0;
        RebuildAvailableSites();
        Chips.CollectionChanged += OnChipsChanged;
    }

    private void OnChipsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        IsEmpty = Chips.Count == 0;
        RebuildAvailableSites();
        if (_suppressWriteback) return;
        WriteBackToConfig();
        _onDirty();
    }

    private void WriteBackToConfig()
    {
        _configList.Clear();
        foreach (var chip in Chips) _configList.Add(chip.SiteName);
    }

    private void RebuildAvailableSites()
    {
        AddableSites.Clear();
        OtherSites.Clear();
        foreach (var pair in _websiteMap)
        {
            var name = pair.Key;
            var site = pair.Value;
            var alreadyAdded = Chips.Any(c => string.Equals(c.SiteName, name, StringComparison.Ordinal));
            var compatible = Category == TopCategory.Unknown || site.TopCategories.Contains(Category);
            if (!alreadyAdded && compatible)
            {
                // 可添加项：副标用"支持：xxx"指明此站点擅长哪些分类
                var supports = string.Join("/", site.TopCategories.Select(TopCategoryDisplayName));
                var subtitle = string.IsNullOrEmpty(supports) ? "通用识别源" : $"支持：{supports}";
                AddableSites.Add(new AddableSiteOption(name, true, subtitle));
            }
            else
            {
                string reason;
                if (alreadyAdded) reason = "已添加";
                else
                {
                    var supports = string.Join("/", site.TopCategories.Select(TopCategoryDisplayName));
                    reason = string.IsNullOrEmpty(supports) ? "不支持此分类" : $"仅支持：{supports}";
                }
                OtherSites.Add(new AddableSiteOption(name, false, reason));
            }
        }
        HasAddable = AddableSites.Count > 0;
        HasOthers = OtherSites.Count > 0;
        OnPropertyChanged(nameof(EmptyFlyoutMessage));
        OnPropertyChanged(nameof(EmptyFlyoutIsWarning));
    }

    [RelayCommand]
    private void AddSite(string? siteName)
    {
        if (string.IsNullOrEmpty(siteName)) return;
        var opt = AddableSites.FirstOrDefault(s => string.Equals(s.Name, siteName, StringComparison.Ordinal));
        if (opt is null || !opt.IsAddable) return;
        Chips.Add(new PriorityChipViewModel(this, siteName));
    }

    public void RemoveChip(PriorityChipViewModel chip)
    {
        Chips.Remove(chip);
    }

    /// <summary>横向 reorder 拖拽用——同 row 内 from→to。从 axaml.cs 调。</summary>
    public void MoveChip(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || from >= Chips.Count) return;
        if (to < 0 || to >= Chips.Count) return;
        Chips.Move(from, to);
    }

    private static string TopCategoryDisplayName(TopCategory cat) => cat switch
    {
        TopCategory.Video => "视频",
        TopCategory.Audio => "音频",
        TopCategory.Picture => "图片",
        TopCategory.Text => "文字",
        TopCategory.Game => "游戏",
        TopCategory.Unknown => "未知",
        _ => cat.ToString(),
    };

    private static IBrush? ResolveBrush(string key)
    {
        if (Application.Current is null) return null;
        if (Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) && v is IBrush b)
            return b;
        return null;
    }

    private static Geometry? ResolveGeometry(string key)
    {
        if (Application.Current is null) return null;
        if (Application.Current.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) && v is Geometry g)
            return g;
        return null;
    }
}

/// <summary>chip：站点名 + 持父 Row 引用（用于 Remove）</summary>
public partial class PriorityChipViewModel : ObservableObject
{
    public PriorityRowViewModel Row { get; }
    public string SiteName { get; }

    public PriorityChipViewModel(PriorityRowViewModel row, string siteName)
    {
        Row = row;
        SiteName = siteName;
    }

    [RelayCommand]
    private void Remove() => Row.RemoveChip(this);
}

/// <summary>"+ 添加" Flyout 项。IsAddable=false 时点击 no-op，UnavailableReason 用于次行灰字。</summary>
public record AddableSiteOption(string Name, bool IsAddable, string? UnavailableReason);

public record SteamLanguageOption(string Code, string Display);

public record SteamCountryOption(string Code, string Display);
