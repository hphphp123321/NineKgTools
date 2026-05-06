using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Services.Configs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 识别网站配置页。Phase 2.2 MVP：三张网站状态卡 + 启用开关 + 凭证字段。
/// 拖拽优先级（5 分类×N 网站）留给下一轮——AXAML DragDrop 实现量大，先把"配置 ApiKey"高频路径走通。
/// </summary>
public partial class WebsitesViewModel : PageViewModelBase
{
    private readonly Config _config;
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

    public WebsitesViewModel(Config config)
    {
        _config = config;
        LoadFromConfig();
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
                // 强制兜底——cn 不允许
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
        // 防御：UI 上 cn 项已 disabled，但万一被绑定上来强制兜底
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

public record SteamLanguageOption(string Code, string Display);

public record SteamCountryOption(string Code, string Display);
