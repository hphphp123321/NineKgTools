using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Auth;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Tasks;
using Serilog;

namespace NineKgTools.Pages.Settings;

public partial class Settings : ComponentBase
{
    [Inject] private Config Config { get; set; }
    [Inject] private OpenaiService OpenaiService { get; set; }
    [Inject] private AuthService AuthService { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private UnifiedTaskService UnifiedTaskService { get; set; } = null!;

    // 配置对象
    private AppConfig _appConfig = new();
    private AIConfig _aiConfig = new();
    private FilesConfig _filesConfig = new();
    private LogConfig _logConfig = new();
    private TaskConfig _tasksConfig = new();
    private TagMatchingConfig _tagMatchingConfig = new();
    private SearchConfig _searchConfig = new();
    private IdentificationConfig _identificationConfig = new();

    // UI状态变量
    private bool _saveProcessing = false;
    private bool _testAIProcessing = false;
    private bool _listModelsProcessing = false;
    private bool _tabSaveProcessing = false;


    // 账号管理相关变量
    private string _currentUsername = "";
    private DateTime? _lastLoginAt;
    private string _changeUsernameCurrentPassword = "";
    private string _newUsername = "";
    private string _changePasswordCurrentPassword = "";
    private string _newPassword = "";
    private string _confirmNewPassword = "";
    private bool _changeUsernameProcessing = false;
    private bool _changePasswordProcessing = false;
    private bool _showChangeUsernameDialog = false;
    private bool _showChangePasswordDialog = false;

    private bool _isPasswordFormValid =>
        !string.IsNullOrWhiteSpace(_changePasswordCurrentPassword) &&
        !string.IsNullOrWhiteSpace(_newPassword) &&
        !string.IsNullOrWhiteSpace(_confirmNewPassword) &&
        _newPassword == _confirmNewPassword &&
        _newPassword.Length >= 4;
    
    // 多选相关属性
    private IEnumerable<LogType> _selectedLogTypes = new List<LogType>();
    private IEnumerable<string> _selectedIgnoredFiles = new List<string>();
    private IEnumerable<string> _selectedIgnoredPatterns = new List<string>();
    private IEnumerable<string> _selectedAllowedExtensions = new List<string>();
    private bool _testProxyProcessing = false;
    private int _activeTabIndex = 0;
    private bool _isTabTransitioning = false;

    // 表单相关变量
    private string _newIgnoredFile = "";
    private string _newIgnoredPattern = "";
    private string _newAllowedExtension = "";

    // 默认选项列表
    private readonly List<string> _defaultIgnoredFiles = new()
    {
        "Thumbs.db", ".DS_Store", "desktop.ini", ".gitkeep", ".gitignore"
    };
    private readonly List<string> _defaultIgnoredPatterns = new()
    {
        ".*", "*.tmp", "*.temp", "*.cache", "*.log", "*.bak", "*.swp", "*.swo"
    };
    private readonly List<string> _defaultAllowedExtensions = new()
    {
        ".mp4", ".mkv", ".avi", ".wmv", ".flv", ".mov", ".m4v", ".webm", ".ts", ".m2ts",
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp",
        ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a"
    };

    protected override async Task OnInitializedAsync()
    {
        // 初始化配置对象
        _appConfig = Config.App.Copy();
        _aiConfig = Config.Ai.Copy();
        _filesConfig = Config.Files.Copy();
        _logConfig = Config.Log.Copy();
        _tasksConfig = Config.Tasks.Copy();
        _tagMatchingConfig = Config.TagMatching.Copy();
        _searchConfig = Config.Search.Copy();
        _identificationConfig = Config.Identification.Copy();

        // 同步多选状态
        _selectedLogTypes = _logConfig.LogTypes?.ToList() ?? new List<LogType>();
        _selectedIgnoredFiles = _filesConfig.IgnoredFiles?.ToList() ?? new List<string>();
        _selectedIgnoredPatterns = _filesConfig.IgnoredPatterns?.ToList() ?? new List<string>();
        _selectedAllowedExtensions = _filesConfig.AllowedExtensions?.ToList() ?? new List<string>();

        // 加载当前用户信息
        await LoadCurrentUser();

        await base.OnInitializedAsync();
    }

    #region 保存和重置方法

    /// <summary>
    /// 保存全部配置
    /// </summary>
    private async Task _saveAllConfig()
    {
        if (_saveProcessing) return;
        _saveProcessing = true;
        Log.Debug("开始保存全部配置项");

        try
        {
            // 同步多选状态回配置对象
            _logConfig.LogTypes = _selectedLogTypes;
            _filesConfig.IgnoredFiles = _selectedIgnoredFiles.ToList();
            _filesConfig.IgnoredPatterns = _selectedIgnoredPatterns.ToList();
            _filesConfig.AllowedExtensions = _selectedAllowedExtensions.ToList();

            // 保存所有配置
            Config.App = _appConfig.Copy();
            Config.Ai = _aiConfig.Copy();
            Config.Files = _filesConfig.Copy();
            Config.Log = _logConfig.Copy();
            Config.Tasks = _tasksConfig.Copy();
            Config.TagMatching = _tagMatchingConfig.Copy();
            Config.Search = _searchConfig.Copy();
            Config.Identification = _identificationConfig.Copy();

            await Config.SaveConfig();
            Snackbar.Add("所有配置保存成功", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存配置时出错");
            Snackbar.Add($"保存配置失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saveProcessing = false;
        }
    }

    /// <summary>
    /// 重置全部配置
    /// </summary>
    private void _resetAllConfig()
    {
        Log.Debug("开始重置全部配置项");

        try
        {
            _appConfig = Config.App.Copy();
            _aiConfig = Config.Ai.Copy();
            _filesConfig = Config.Files.Copy();
            _logConfig = Config.Log.Copy();
            _tasksConfig = Config.Tasks.Copy();
            _tagMatchingConfig = Config.TagMatching.Copy();
            _searchConfig = Config.Search.Copy();
            _identificationConfig = Config.Identification.Copy();

            _selectedLogTypes = _logConfig.LogTypes?.ToList() ?? new List<LogType>();
            _selectedIgnoredFiles = _filesConfig.IgnoredFiles?.ToList() ?? new List<string>();
            _selectedIgnoredPatterns = _filesConfig.IgnoredPatterns?.ToList() ?? new List<string>();
            _selectedAllowedExtensions = _filesConfig.AllowedExtensions?.ToList() ?? new List<string>();

            Snackbar.Add("所有配置已重置", Severity.Info);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重置配置时出错");
            Snackbar.Add($"重置配置失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 保存单个标签页的配置
    /// </summary>
    private async Task _saveTabConfig(string tabName)
    {
        if (_tabSaveProcessing) return;
        _tabSaveProcessing = true;
        Log.Debug("保存{TabName}配置", tabName);

        try
        {
            switch (tabName.ToLower())
            {
                case "app":
                    Config.App = _appConfig.Copy();
                    break;
                case "log":
                    // 同步多选状态回配置
                    _logConfig.LogTypes = _selectedLogTypes;
                    Config.Log = _logConfig.Copy();
                    break;
                case "ai":
                    await SaveAiConfigWithSyncCheck();
                    return; // 已经在方法内处理了保存和提示
                case "files":
                    // 同步多选状态回配置
                    _filesConfig.IgnoredFiles = _selectedIgnoredFiles.ToList();
                    _filesConfig.IgnoredPatterns = _selectedIgnoredPatterns.ToList();
                    _filesConfig.AllowedExtensions = _selectedAllowedExtensions.ToList();
                    Config.Files = _filesConfig.Copy();
                    break;
                case "tasks":
                    // 定时任务配置已移至定时任务页面，这里只保存缓存清理等配置
                    Config.Tasks = _tasksConfig.Copy();
                    break;
                case "tag_matching":
                    Config.TagMatching = _tagMatchingConfig.Copy();
                    break;
                case "search":
                    Config.Search = _searchConfig.Copy();
                    break;
                case "identification":
                    Config.Identification = _identificationConfig.Copy();
                    break;
                default:
                    throw new ArgumentException($"未知的配置标签页: {tabName}");
            }

            await Config.SaveConfig();
            Snackbar.Add($"{_getTabDisplayName(tabName)}保存成功", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存{TabName}配置时出错", tabName);
            Snackbar.Add($"保存{_getTabDisplayName(tabName)}失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _tabSaveProcessing = false;
        }
    }

    /// <summary>
    /// 保存AI配置并检测是否需要触发向量同步
    /// </summary>
    private async Task SaveAiConfigWithSyncCheck()
    {
        try
        {
            // 记录保存前的向量配置状态
            var oldUseAi = Config.Ai?.UseAi ?? false;
            var oldVectorEnable = Config.Ai?.Vector?.Enable ?? false;
            var oldMediaVectorEnable = Config.Ai?.Vector?.Media?.Enable ?? false;
            var oldTagVectorEnable = Config.Ai?.Vector?.Tag?.Enable ?? false;

            // 获取新的配置状态
            var newUseAi = _aiConfig.UseAi;
            var newVectorEnable = _aiConfig.Vector?.Enable ?? false;
            var newMediaVectorEnable = _aiConfig.Vector?.Media?.Enable ?? false;
            var newTagVectorEnable = _aiConfig.Vector?.Tag?.Enable ?? false;

            // 保存配置
            Config.Ai = _aiConfig.Copy();
            await Config.SaveConfig();
            Snackbar.Add("AI配置保存成功", Severity.Success);

            // 检测是否需要触发同步
            var needMediaSync = false;
            var needTagSync = false;

            // 检测AI从禁用变为启用
            if (!oldUseAi && newUseAi && newVectorEnable)
            {
                needMediaSync = newMediaVectorEnable;
                needTagSync = newTagVectorEnable;
            }
            // 检测向量存储从禁用变为启用
            else if (oldUseAi && newUseAi && !oldVectorEnable && newVectorEnable)
            {
                needMediaSync = newMediaVectorEnable;
                needTagSync = newTagVectorEnable;
            }
            // 检测媒体向量从禁用变为启用
            else if (oldUseAi && newUseAi && oldVectorEnable && newVectorEnable)
            {
                if (!oldMediaVectorEnable && newMediaVectorEnable)
                {
                    needMediaSync = true;
                }
                if (!oldTagVectorEnable && newTagVectorEnable)
                {
                    needTagSync = true;
                }
            }

            // 如果需要同步，弹窗询问用户
            if (needMediaSync || needTagSync)
            {
                var syncItems = new List<string>();
                if (needTagSync) syncItems.Add("标签向量");
                if (needMediaSync) syncItems.Add("媒体向量");

                var confirmed = await NineKgConfirmDialog.ShowAsync(
                    DialogService,
                    "检测到向量功能已启用",
                    $"是否立即同步 {string.Join(" 和 ", syncItems)}？同步将在后台执行，可在任务管理页查看进度。",
                    intent: ConfirmIntent.Info,
                    confirmText: "立即同步",
                    cancelText: "稍后处理",
                    icon: Icons.Material.Filled.Sync,
                    heroSubtitle: "AI 向量功能刚被开启");

                if (confirmed)
                {
                    await TriggerVectorSyncAsync(needTagSync, needMediaSync);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存AI配置时出错");
            Snackbar.Add("保存 AI 配置失败，请稍后重试。", Severity.Error);
        }
    }

    /// <summary>
    /// 触发向量同步任务
    /// </summary>
    private async Task TriggerVectorSyncAsync(bool syncTags, bool syncMedia)
    {
        try
        {
            var tasks = new List<string>();

            if (syncTags)
            {
                await UnifiedTaskService.ExecuteScheduledTaskAsync("TagVectorSync", CancellationToken.None);
                tasks.Add("标签向量同步");
            }

            if (syncMedia)
            {
                await UnifiedTaskService.ExecuteScheduledTaskAsync("MediaVectorSync", CancellationToken.None);
                tasks.Add("媒体向量同步");
            }

            if (tasks.Any())
            {
                Snackbar.Add($"已提交同步任务: {string.Join(", ", tasks)}", Severity.Info);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "触发向量同步任务时出错");
            Snackbar.Add($"同步任务提交失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 获取标签页显示名称
    /// </summary>
    private string _getTabDisplayName(string tabName) => tabName.ToLower() switch
    {
        "app" => "应用设置",
        "log" => "日志配置",
        "ai" => "AI配置",
        "files" => "文件配置",
        "tasks" => "任务配置",
        "tag_matching" => "标签匹配配置",
        "search" => "搜索配置",
        "identification" => "识别配置",
        _ => "配置"
    };

    #endregion

    #region 交互方法

    /// <summary>
    /// 切换标签页（带过渡动画）
    /// </summary>
    private async Task _onTabChanged(int newIndex)
    {
        if (newIndex == _activeTabIndex || _isTabTransitioning) return;

        // 淡出
        _isTabTransitioning = true;
        StateHasChanged();

        // 等待淡出完成（与 CSS transition duration 一致）
        await Task.Delay(160);

        // 切换内容
        _activeTabIndex = newIndex;
        _isTabTransitioning = false;
        StateHasChanged();
    }

    /// <summary>
    /// 获取浏览器User-Agent
    /// </summary>
    private async Task _getUserAgent()
    {
        try
        {
            var remoteUserAgent = await JsRuntime.InvokeAsync<string>("getUserAgent", null);
            Log.Debug("获取到浏览器User-Agent: {UserAgent}", remoteUserAgent);
            _appConfig.UserAgent = remoteUserAgent;
            Snackbar.Add("User-Agent已更新", Severity.Success);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取User-Agent时出错");
            Snackbar.Add("获取User-Agent失败", Severity.Error);
        }
    }

    /// <summary>
    /// 测试代理连接
    /// </summary>
    private async Task _testProxy()
    {
        var proxyAddr = _appConfig.Proxy.ProxyAddr?.Trim();
        if (string.IsNullOrEmpty(proxyAddr))
        {
            Snackbar.Add("代理地址为空，无需代理", Severity.Info);
            return;
        }

        if (!Uri.TryCreate(proxyAddr, UriKind.Absolute, out _))
        {
            Snackbar.Add("代理地址格式无效，请输入完整地址（如 http://127.0.0.1:7890）", Severity.Warning);
            return;
        }

        if (_testProxyProcessing) return;
        _testProxyProcessing = true;
        Log.Debug("开始测试代理连接: {ProxyAddr}", proxyAddr);

        try
        {
            using var handler = new HttpClientHandler();
            handler.Proxy = new System.Net.WebProxy(proxyAddr);
            if (!string.IsNullOrEmpty(_appConfig.Proxy.ProxyUser))
            {
                handler.Proxy.Credentials = new System.Net.NetworkCredential(
                    _appConfig.Proxy.ProxyUser, _appConfig.Proxy.ProxyPassword);
            }

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.GetAsync("https://www.google.com/generate_204");

            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("代理连接测试成功", Severity.Success);
            }
            else
            {
                Snackbar.Add($"代理连接返回状态码: {(int)response.StatusCode}", Severity.Warning);
            }
        }
        catch (TaskCanceledException)
        {
            Snackbar.Add("代理连接超时", Severity.Error);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "测试代理连接时出错");
            Snackbar.Add($"代理连接测试失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _testProxyProcessing = false;
        }
    }

    /// <summary>
    /// 添加忽略文件
    /// </summary>
    private void _addIgnoredFile()
    {
        var fileName = _newIgnoredFile?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Snackbar.Add("请输入文件名", Severity.Warning);
            return;
        }

        if (_selectedIgnoredFiles.Contains(fileName) || _filesConfig.IgnoredFiles?.Contains(fileName) == true)
        {
            Snackbar.Add("该文件已在忽略列表中", Severity.Warning);
            return;
        }

        // 同时更新配置和默认列表（供下拉选择）
        _filesConfig.IgnoredFiles ??= new List<string>();
        _filesConfig.IgnoredFiles.Add(fileName);
        if (!_defaultIgnoredFiles.Contains(fileName))
            _defaultIgnoredFiles.Add(fileName);
        _selectedIgnoredFiles = _selectedIgnoredFiles.Append(fileName).ToList();
        _newIgnoredFile = "";
        Snackbar.Add("忽略文件已添加", Severity.Success);
        StateHasChanged();
    }

    /// <summary>
    /// 添加忽略模式
    /// </summary>
    private void _addIgnoredPattern()
    {
        var pattern = _newIgnoredPattern?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(pattern))
        {
            Snackbar.Add("请输入文件模式", Severity.Warning);
            return;
        }

        if (_selectedIgnoredPatterns.Contains(pattern) || _filesConfig.IgnoredPatterns?.Contains(pattern) == true)
        {
            Snackbar.Add("该模式已在忽略列表中", Severity.Warning);
            return;
        }

        _filesConfig.IgnoredPatterns ??= new List<string>();
        _filesConfig.IgnoredPatterns.Add(pattern);
        if (!_defaultIgnoredPatterns.Contains(pattern))
            _defaultIgnoredPatterns.Add(pattern);
        _selectedIgnoredPatterns = _selectedIgnoredPatterns.Append(pattern).ToList();
        _newIgnoredPattern = "";
        Snackbar.Add("忽略模式已添加", Severity.Success);
        StateHasChanged();
    }

    /// <summary>
    /// 添加允许的扩展名
    /// </summary>
    private void _addAllowedExtension()
    {
        if (string.IsNullOrWhiteSpace(_newAllowedExtension))
        {
            Snackbar.Add("请输入扩展名", Severity.Warning);
            return;
        }

        // 确保扩展名以点开头
        var extension = _newAllowedExtension.Trim();
        if (!extension.StartsWith("."))
        {
            extension = "." + extension;
        }
        extension = extension.ToLowerInvariant();

        if (_selectedAllowedExtensions.Contains(extension) || _filesConfig.AllowedExtensions?.Contains(extension) == true)
        {
            Snackbar.Add("该扩展名已在允许列表中", Severity.Warning);
            return;
        }

        _filesConfig.AllowedExtensions ??= new List<string>();
        _filesConfig.AllowedExtensions.Add(extension);
        if (!_defaultAllowedExtensions.Contains(extension))
            _defaultAllowedExtensions.Add(extension);
        _selectedAllowedExtensions = _selectedAllowedExtensions.Append(extension).ToList();
        _newAllowedExtension = "";
        Snackbar.Add("允许的扩展名已添加", Severity.Success);
        StateHasChanged();
    }

    #endregion

    #region AI相关方法

    /// <summary>
    /// 测试AI连接
    /// </summary>
    private async Task _testAI()
    {
        if (_testAIProcessing) return;
        _testAIProcessing = true;
        Log.Debug("开始测试AI连接");

        try
        {
            // 仅保存AI配置用于测试，不保存全部配置
            Config.Ai = _aiConfig.Copy();
            await Config.SaveConfig();

            await OpenaiService.Init();
            var resp = await OpenaiService.Chat("hello")!;
            Log.Debug("AI 测试结果: {Resp}", resp);

            if (string.IsNullOrEmpty(resp))
            {
                Snackbar.Add("AI 测试失败, 请检查相关设置", Severity.Error);
            }
            else
            {
                Snackbar.Add("AI 测试成功", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "测试AI连接时出错");
            Snackbar.Add($"AI连接测试失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _testAIProcessing = false;
        }
    }

    /// <summary>
    /// 手动刷新AI模型列表
    /// </summary>
    internal async Task RefreshAIModels()
    {
        if (_listModelsProcessing) return;
        _listModelsProcessing = true;
        StateHasChanged();
        
        try 
        {
            var models = await OpenaiService.GetModelList(CancellationToken.None);
            if (models.Count == 0)
            {
                Snackbar.Add("获取AI模型列表失败，请检查API配置", Severity.Warning);
            }
            else
            {
                Snackbar.Add($"成功获取{models.Count}个AI模型", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "刷新AI模型列表出错");
            Snackbar.Add("获取AI模型列表出错: " + ex.Message, Severity.Error);
        }
        finally
        {
            _listModelsProcessing = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 获取AI模型列表（用于自动完成）
    /// </summary>
    private async Task<IEnumerable<string>> _listAIModels(string value, CancellationToken token)
    {
        if (!_aiConfig.UseAi)
            return Array.Empty<string>();

        try
        {
            var models = await OpenaiService.GetModelList(token);
            if (models.Count == 0 && !token.IsCancellationRequested)
            {
                Snackbar.Add("AI 服务不可用, 请检查配置项", Severity.Warning);
                return Array.Empty<string>();
            }

            return models.Where(option => option.Contains(value, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取AI模型列表时出错");
            return Array.Empty<string>();
        }
    }

    #endregion

    #region 提示文本方法

    /// <summary>
    /// 获取媒体向量索引的提示文本
    /// </summary>
    private string GetMediaVectorIndexingTipText()
    {
        if (!_aiConfig.UseAi)
        {
            return "需要先启用AI功能才能使用媒体向量索引。启用后可以使用语义搜索功能，提供更智能的媒体搜索体验。";
        }

        if (_aiConfig.Vector?.Enable != true)
        {
            return "需要先在AI配置中启用向量存储才能使用媒体向量索引。启用后可以使用语义搜索功能，提供更智能的媒体搜索体验。";
        }

        return "启用后可以使用语义搜索功能，通过AI技术提供更智能、更精准的媒体搜索体验。系统会为媒体内容创建向量表示，支持基于语义相似性的搜索。";
    }

    /// <summary>
    /// 获取标签向量匹配的提示文本
    /// </summary>
    private string GetTagVectorMatchingTipText()
    {
        if (!_aiConfig.UseAi)
        {
            return "需要先启用AI功能才能使用向量匹配。启用后可以通过AI技术实现更智能的标签匹配，提高标签识别的准确性。";
        }

        if (_aiConfig.Vector?.Enable != true)
        {
            return "需要先在AI配置中启用向量存储才能使用向量匹配。启用后可以通过AI技术实现更智能的标签匹配，提高标签识别的准确性。";
        }

        return "启用后可以通过AI向量技术实现更智能的标签匹配。系统会将标签转换为向量表示，通过计算向量相似度来找到最匹配的标签，提高标签识别的准确性和智能化程度。";
    }

    /// <summary>
    /// 获取媒体向量搜索的提示文本
    /// </summary>
    private string GetVectorSearchForMediaTipText()
    {
        if (!_aiConfig.UseAi)
        {
            return "需要先启用AI功能才能使用媒体向量搜索。启用后可以通过语义理解进行更智能的媒体搜索，找到语义相关的内容。";
        }

        if (_aiConfig.Vector?.Enable != true)
        {
            return "需要先在AI配置中启用向量存储才能使用媒体向量搜索。启用后可以通过语义理解进行更智能的媒体搜索，找到语义相关的内容。";
        }

        return "启用后可以通过AI向量技术进行语义化的媒体搜索。系统会理解搜索意图，找到语义相关的媒体内容，即使关键词不完全匹配也能找到相关结果。";
    }

    /// <summary>
    /// 获取标签向量搜索的提示文本
    /// </summary>
    private string GetVectorSearchForTagsTipText()
    {
        if (!_aiConfig.UseAi)
        {
            return "需要先启用AI功能才能使用标签向量搜索。启用后可以通过语义理解进行更智能的标签搜索，找到语义相关的标签。";
        }

        if (_aiConfig.Vector?.Enable != true)
        {
            return "需要先在AI配置中启用向量存储才能使用标签向量搜索。启用后可以通过语义理解进行更智能的标签搜索，找到语义相关的标签。";
        }

        return "启用后可以通过AI向量技术进行语义化的标签搜索。系统会理解搜索意图，找到语义相关的标签，提供更智能的标签发现和匹配功能。";
    }

    #endregion

    #region 搜索相关方法

    /// <summary>
    /// 测试搜索功能
    /// </summary>
    private void _testSearch()
    {
        if (!_searchConfig.EnableGlobalSearch)
        {
            Snackbar.Add("全局搜索功能已禁用", Severity.Info);
            return;
        }

        Snackbar.Add("搜索配置已就绪", Severity.Success);
    }

    #endregion

    #region 账号管理方法

    /// <summary>
    /// 加载当前用户信息
    /// </summary>
    /// <remarks>
    /// Blazor Server 下服务端 HttpClient 不会带浏览器 Cookie，
    /// 走 <c>/api/auth/current-user</c> 会被 [Authorize] 返回 401；直接调 <see cref="AuthService"/> 即可。
    /// </remarks>
    private async Task LoadCurrentUser()
    {
        try
        {
            var user = await AuthService.GetCurrentUserAsync();
            if (user != null)
            {
                _currentUsername = string.IsNullOrWhiteSpace(user.Username) ? "未知" : user.Username;
                _lastLoginAt = user.LastLoginAt;
            }
            else
            {
                _currentUsername = "未知";
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "获取当前用户信息失败");
            _currentUsername = "未知";
        }
    }

    /// <summary>
    /// 修改用户名
    /// </summary>
    /// <remarks>
    /// 走浏览器 fetch（authInterop.changeUsername）让 Cookie 自动携带；
    /// Blazor Server 下服务端 HttpClient 不带 Cookie，直接打 [Authorize] 接口会 401 + 空响应体。
    /// </remarks>
    private async Task _changeUsername()
    {
        var trimmedUsername = _newUsername?.Trim() ?? "";
        if (trimmedUsername.Length < 2 || trimmedUsername.Length > 50)
        {
            Snackbar.Add("用户名长度需在2-50个字符之间", Severity.Warning);
            return;
        }

        if (_changeUsernameProcessing) return;
        _changeUsernameProcessing = true;
        try
        {
            var result = await JsRuntime.InvokeAsync<AuthInteropResult>(
                "authInterop.changeUsername",
                new object[] { _changeUsernameCurrentPassword, trimmedUsername });

            if (result.Success)
            {
                Snackbar.Add("用户名修改成功，请重新登录", Severity.Success);
                NavigationManager.NavigateTo("/login", true);
            }
            else
            {
                Snackbar.Add(string.IsNullOrWhiteSpace(result.Message) ? "修改失败" : result.Message, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "修改用户名失败");
            Snackbar.Add("修改用户名失败，请检查网络连接", Severity.Error);
        }
        finally
        {
            _changeUsernameProcessing = false;
            _changeUsernameCurrentPassword = "";
            _newUsername = "";
        }
    }

    /// <summary>
    /// 修改密码
    /// </summary>
    /// <remarks>同 <see cref="_changeUsername"/>，走 JS 发起以便携带认证 Cookie。</remarks>
    private async Task _changePassword()
    {
        if (_newPassword != _confirmNewPassword)
        {
            Snackbar.Add("两次输入的新密码不一致", Severity.Warning);
            return;
        }

        if (_newPassword.Length < 4)
        {
            Snackbar.Add("新密码长度至少为4个字符", Severity.Warning);
            return;
        }

        if (_changePasswordProcessing) return;
        _changePasswordProcessing = true;
        try
        {
            var result = await JsRuntime.InvokeAsync<AuthInteropResult>(
                "authInterop.changePassword",
                new object[] { _changePasswordCurrentPassword, _newPassword });

            if (result.Success)
            {
                Snackbar.Add("密码修改成功，请重新登录", Severity.Success);
                NavigationManager.NavigateTo("/login", true);
            }
            else
            {
                Snackbar.Add(string.IsNullOrWhiteSpace(result.Message) ? "修改失败" : result.Message, Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "修改密码失败");
            Snackbar.Add("修改密码失败，请检查网络连接", Severity.Error);
        }
        finally
        {
            _changePasswordProcessing = false;
            _changePasswordCurrentPassword = "";
            _newPassword = "";
            _confirmNewPassword = "";
        }
    }

    /// <summary>
    /// authInterop 返回的统一结果结构（字段走 Web JSON 默认，大小写不敏感）
    /// </summary>
    private class AuthInteropResult
    {
        public bool Success { get; set; }
        public int Status { get; set; }
        public string? Message { get; set; }
    }

    #endregion
}