using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// 首次启动引导（3 步）VM。Step1 欢迎 + 数据目录；Step2 添加监视文件夹；Step3 识别源说明 + Bangumi ApiKey。
/// 每步都可「跳过」直接完成。完成 / 跳过都通过 <see cref="RequestClose"/> 关闭对话框，
/// 由调用方（App）落 <see cref="Services.DesktopPreferences.FirstRunCompleted"/>=true。
///
/// 文件夹选择器需要 TopLevel，放在 code-behind 取（<see cref="Views.Dialogs.FirstRunWizardDialog"/>），
/// 回来后调 <see cref="TryAddWatchFolderAsync"/>——与 WatchFoldersViewModel.AddPathInternalAsync 同语义
/// （写 config + 落盘 + 立即 IdentifyBatchMedia 挂监控）。
/// </summary>
public partial class FirstRunWizardContext : ObservableObject
{
    private readonly Config _config;
    private readonly FilesService _filesService;

    /// <summary>数据目录路径（Step1 展示 + 打开）。</summary>
    public string DataDirectory { get; }

    /// <summary>完成 / 跳过时触发，让 code-behind 关闭 FAContentDialog。</summary>
    public event Action? RequestClose;

    public FirstRunWizardContext(Config config, FilesService filesService, string dataDir)
    {
        _config = config;
        _filesService = filesService;
        DataDirectory = dataDir;

        foreach (var f in _config.Source?.WatchFolders ?? new List<string>())
            WatchFolders.Add(f);
        WatchFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasWatchFolders));

        BangumiApiKey = _config.Website?.Bangumi?.ApiKey ?? "";
        DlsiteEnabled = _config.Website?.DLsite?.Enable ?? true;
        SteamEnabled = _config.Website?.Steam?.Enable ?? true;
        BangumiEnabled = _config.Website?.Bangumi?.Enable ?? false;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(CanGoPrev))]
    [NotifyPropertyChangedFor(nameof(CanGoNext))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(StepIndicatorText))]
    private int _currentStep = 1;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool CanGoPrev => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < 3;
    public bool IsLastStep => CurrentStep == 3;
    public string StepIndicatorText => $"第 {CurrentStep} / 3 步";

    // ===== Step2：监视文件夹 =====
    public ObservableCollection<string> WatchFolders { get; } = new();
    public bool HasWatchFolders => WatchFolders.Count > 0;

    // ===== Step3：识别源 =====
    public bool DlsiteEnabled { get; }
    public bool SteamEnabled { get; }
    public bool BangumiEnabled { get; }

    [ObservableProperty]
    private string _bangumiApiKey = "";

    [RelayCommand]
    private void Prev()
    {
        if (CanGoPrev) CurrentStep--;
    }

    [RelayCommand]
    private void Next()
    {
        if (CanGoNext) CurrentStep++;
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        try { Process.Start("explorer.exe", DataDirectory); }
        catch (Exception ex) { Log.Warning(ex, "首次引导：打开数据目录失败"); }
    }

    /// <summary>「跳过」：不再往后走，直接按完成处理。</summary>
    [RelayCommand]
    private Task Skip() => FinishInternalAsync();

    /// <summary>「完成」：保存 Bangumi ApiKey（若改动）后关闭。</summary>
    [RelayCommand]
    private Task Finish() => FinishInternalAsync();

    private async Task FinishInternalAsync()
    {
        try
        {
            var key = BangumiApiKey?.Trim() ?? "";
            if (_config.Website?.Bangumi is { } b && b.ApiKey != key)
            {
                b.ApiKey = key;
                // 填了 key 顺手启用 Bangumi（否则填了也不生效，违反用户预期）
                if (!string.IsNullOrEmpty(key)) b.Enable = true;
                await _config.SaveConfig();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "首次引导：保存 Bangumi ApiKey 失败");
        }
        RequestClose?.Invoke();
    }

    /// <summary>code-behind 文件夹选择回来后调用：去重 → 写 config + 落盘 → 立即提交批量识别（挂监控）。</summary>
    public async Task TryAddWatchFolderAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (WatchFolders.Contains(path)) return;

        _config.Source ??= new SourceConfig();
        if (!_config.Source.WatchFolders.Contains(path))
            _config.Source.WatchFolders.Add(path);

        WatchFolders.Add(path);

        try
        {
            await _config.SaveConfig();
            // 与 WatchFoldersViewModel 一致：加入即扫已有 + 挂 FileSystemWatcher
            await _filesService.IdentifyBatchMedia(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "首次引导：添加监视文件夹失败 {Path}", path);
        }
    }
}
