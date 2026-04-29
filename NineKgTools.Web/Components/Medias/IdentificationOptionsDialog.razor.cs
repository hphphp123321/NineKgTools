using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Websites;
using NineKgTools.Core.Models.Categories;
using Serilog;

namespace NineKgTools.Components.Medias;

public partial class IdentificationOptionsDialog
{
    [Inject] private Config Config { get; set; } = null!;
    [Inject] private WebsiteService WebsiteService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter] public string? SourcePath { get; set; }
    [Parameter] public IdentificationOptions? InitialOptions { get; set; }

    // UI状态字段（用于双向绑定）
    private string? _preferredWebsite;
    private string? _websiteSpecificId;
    private string? _customIdentificationName;
    private Dictionary<string, string> _websiteIds = new();
    private bool _skipCache;
    private int _timeoutSeconds;
    private IdentificationStrategy _strategy;
    private IEnumerable<string>? _websitePriorityOverride;
    private TopCategory? _suggestedCategory;

    // 辅助字段
    private IdentificationOptions _currentOptions = new();
    private List<string> _availableWebsites = new();
    private string _newWebsiteName = "";
    private string _newWebsiteId = "";

    protected override void OnInitialized()
    {
        try
        {
            // 1. 从Config加载默认配置
            _currentOptions = Config.Identification.ToIdentificationOptions();

            // 2. 如果有InitialOptions，覆盖默认值
            if (InitialOptions != null)
            {
                _currentOptions = InitialOptions;
            }

            // 3. 设置SourcePath
            _currentOptions.SourcePath = SourcePath;

            // 4. 获取可用网站列表
            _availableWebsites = WebsiteService.WebsiteNameMap.Keys.ToList();

            // 5. 从CurrentOptions恢复UI状态
            RestoreUIFromOptions();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化IdentificationOptionsDialog失败");
            Snackbar.Add("初始化失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 从IdentificationOptions恢复UI状态
    /// </summary>
    private void RestoreUIFromOptions()
    {
        _preferredWebsite = _currentOptions.PreferredWebsite;
        _websiteSpecificId = _currentOptions.WebsiteSpecificId;
        _customIdentificationName = _currentOptions.CustomIdentificationName;
        _websiteIds = _currentOptions.WebsiteIds?.ToDictionary(x => x.Key, x => x.Value) ?? new();
        _skipCache = _currentOptions.SkipCache;
        _timeoutSeconds = (int)_currentOptions.Timeout.TotalSeconds;
        _strategy = _currentOptions.Strategy;
        _websitePriorityOverride = _currentOptions.WebsitePriorityOverride;
        _suggestedCategory = _currentOptions.SuggestedCategory;
    }

    /// <summary>
    /// 从UI状态构建IdentificationOptions对象
    /// </summary>
    private void BuildOptionsFromUI()
    {
        _currentOptions.PreferredWebsite = string.IsNullOrWhiteSpace(_preferredWebsite) ? null : _preferredWebsite;
        _currentOptions.WebsiteSpecificId = string.IsNullOrWhiteSpace(_websiteSpecificId) ? null : _websiteSpecificId;
        _currentOptions.CustomIdentificationName = string.IsNullOrWhiteSpace(_customIdentificationName) ? null : _customIdentificationName;
        _currentOptions.WebsiteIds = _websiteIds.Count > 0 ? new Dictionary<string, string>(_websiteIds) : null;
        _currentOptions.SkipCache = _skipCache;
        _currentOptions.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        _currentOptions.Strategy = _strategy;
        _currentOptions.WebsitePriorityOverride = _websitePriorityOverride?.Any() == true ? _websitePriorityOverride.ToList() : null;
        // 注意：AutoAddToDatabase 不再由此对话框控制，由调用方在 InitialOptions 里写死，
        // 手动识别入口固定传 false，后台识别流程由 Settings 里的全局默认值决定。
        _currentOptions.SuggestedCategory = _suggestedCategory;
        _currentOptions.SourcePath = SourcePath;
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    private bool ValidateOptions()
    {
        BuildOptionsFromUI();

        var validationResult = _currentOptions.Validate();
        if (!validationResult.IsValid)
        {
            Snackbar.Add(validationResult.GetErrorMessage(), Severity.Error);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 重置为默认配置
    /// </summary>
    private void ResetToDefaults()
    {
        try
        {
            // 重新从Config加载默认值
            _currentOptions = Config.Identification.ToIdentificationOptions();
            _currentOptions.SourcePath = SourcePath;

            // 恢复UI状态
            RestoreUIFromOptions();

            // 清空临时输入
            _newWebsiteName = "";
            _newWebsiteId = "";

            Snackbar.Add("已重置为默认配置", Severity.Info);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重置配置失败");
            Snackbar.Add("重置失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 添加网站ID映射
    /// </summary>
    private void AddWebsiteId()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_newWebsiteName))
            {
                Snackbar.Add("请选择网站名称", Severity.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_newWebsiteId))
            {
                Snackbar.Add("请输入网站ID", Severity.Warning);
                return;
            }

            if (_websiteIds.ContainsKey(_newWebsiteName))
            {
                Snackbar.Add($"网站 {_newWebsiteName} 已存在，请先删除旧的映射", Severity.Warning);
                return;
            }

            _websiteIds[_newWebsiteName] = _newWebsiteId;

            // 清空输入
            _newWebsiteName = "";
            _newWebsiteId = "";

            Snackbar.Add("已添加网站ID映射", Severity.Success);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "添加网站ID映射失败 Website={WebsiteName}", _newWebsiteName);
            Snackbar.Add("添加失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 删除网站ID映射
    /// </summary>
    private void RemoveWebsiteId(string websiteName)
    {
        try
        {
            if (_websiteIds.Remove(websiteName))
            {
                Snackbar.Add($"已删除 {websiteName} 的ID映射", Severity.Info);
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除网站ID映射失败 Website={WebsiteName}", websiteName);
            Snackbar.Add("删除失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 取消
    /// </summary>
    private void Cancel()
    {
        MudDialog.Cancel();
    }

    /// <summary>
    /// 确认并开始识别
    /// </summary>
    private void Confirm()
    {
        try
        {
            // 验证配置
            if (!ValidateOptions())
            {
                return;
            }

            // 返回配置好的Options
            MudDialog.Close(DialogResult.Ok(_currentOptions));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "确认识别选项失败");
            Snackbar.Add("确认失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 获取策略的描述文本
    /// </summary>
    private string GetStrategyDescription(IdentificationStrategy strategy)
    {
        return strategy switch
        {
            IdentificationStrategy.Auto => "自动模式，按照默认流程识别",
            IdentificationStrategy.Manual => "手动模式，使用指定的网站和ID",
            IdentificationStrategy.Hybrid => "混合模式，先尝试手动指定，失败后自动识别",
            IdentificationStrategy.ForceRefresh => "强制刷新，忽略缓存，重新识别",
            IdentificationStrategy.CacheOnly => "仅缓存，只从缓存获取，不进行网络请求",
            IdentificationStrategy.Quick => "快速模式，使用更激进的超时和并行策略",
            _ => "未知策略"
        };
    }
}
