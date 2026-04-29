using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Media;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Sources;

/// <summary>
/// 待入库列表项 —— 包含源记录和已反序列化的 MediaBase。
/// </summary>
public record PendingEntry(MediaSource Source, MediaBase Media);

public partial class UnknownPage : ComponentBase
{
    [Inject] private MediaDbContext DbContext { get; set; } = default!;
    [Inject] private MediaService MediaService { get; set; } = default!;
    [Inject] private FilesService FilesService { get; set; } = default!;
    [Inject] private PendingIdentificationService PendingIdentificationService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private Config Config { get; set; } = default!;
    [Inject] private IFileExplorerService FileExplorerService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    /// <summary>
    /// 查询参数 ?tab= —— 支持从主页"待处理"卡片或其它入口直接落到指定 Tab：
    /// "unidentified" → 待识别（index 0），"pending" → 待入库（index 1）。
    /// 缺省或无效值时维持默认行为（0 = 待识别）。大小写不敏感。
    /// </summary>
    [SupplyParameterFromQuery(Name = "tab")]
    public string? Tab { get; set; }

    // 0 = 待识别，1 = 待入库
    private int _activeTabIndex;

    private List<MediaSource> _allUnprocessedSources = new();
    private List<MediaSource> _filteredSources = new();

    private List<PendingEntry> _allPendingSources = new();
    private List<PendingEntry> _filteredPendingSources = new();

    // 正在入库中的 MediaSource Id 集合，支持多行独立 loading 态、防止同一行双击重复触发
    private readonly HashSet<int> _committingSourceIds = new();

    private bool IsCommitting(int sourceId) => _committingSourceIds.Contains(sourceId);

    // 两个 Tab 共享筛选条件
    private TopCategory? _categoryFilter;
    private string _searchText = "";

    // 两个 Tab 的选中项独立维护，切 Tab 时各自清空（OnActiveTabChanged）
    private HashSet<MediaSource> _selectedItems = new();
    private HashSet<PendingEntry> _selectedPendingItems = new();

    // 统计：两个 Tab 各算一份分类计数，CurrentCategoryCounts 暴露当前 Tab 的那份
    private int _totalCount;
    private int _pendingTotalCount;
    private readonly Dictionary<TopCategory, int> _unidentifiedCategoryCounts = new();
    private readonly Dictionary<TopCategory, int> _pendingCategoryCounts = new();

    private IReadOnlyDictionary<TopCategory, int> CurrentCategoryCounts =>
        _activeTabIndex == 0 ? _unidentifiedCategoryCounts : _pendingCategoryCounts;

    private int CurrentTabTotal => _activeTabIndex == 0 ? _totalCount : _pendingTotalCount;

    private bool IsUnidentifiedTab => _activeTabIndex == 0;

    private static readonly TopCategory[] StatCategories =
    [
        TopCategory.Video, TopCategory.Audio, TopCategory.Picture,
        TopCategory.Game, TopCategory.Unknown
    ];

    private static readonly Dictionary<TopCategory, string> CategoryNames = new()
    {
        [TopCategory.Video] = "视频",
        [TopCategory.Audio] = "音频",
        [TopCategory.Picture] = "图片",
        [TopCategory.Text] = "文本",
        [TopCategory.Game] = "游戏",
        [TopCategory.Unknown] = "未知"
    };

    private bool _isLoading;
    private bool _isBatchProcessing;

    private bool IsRemoteAccess => !FileExplorerService.IsLocalAccessSupported;

    protected override async Task OnInitializedAsync()
    {
        ApplyTabQueryParameter();
        await LoadAllAsync();
    }

    /// <summary>
    /// 把 ?tab= 映射成 _activeTabIndex。大小写不敏感；无效值保持默认。
    /// </summary>
    private void ApplyTabQueryParameter()
    {
        if (string.IsNullOrWhiteSpace(Tab))
            return;

        _activeTabIndex = Tab.Trim().ToLowerInvariant() switch
        {
            "pending" => 1,
            "unidentified" => 0,
            _ => _activeTabIndex
        };
    }

    /// <summary>
    /// 加载两个 Tab 的数据
    /// </summary>
    private async Task LoadAllAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            _allUnprocessedSources = await DbContext.MediaSources
                .Where(m => !m.Identified)
                .OrderByDescending(m => m.FullPath)
                .ToListAsync();

            var raw = await PendingIdentificationService.GetAllPendingAsync();
            _allPendingSources = raw.Select(t => new PendingEntry(t.source, t.media)).ToList();

            UpdateStatistics();
            ApplyFilters();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载待处理媒体源失败");
            Snackbar.Add("加载待处理媒体源失败", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 兼容别名，部分回调仍直接调它。
    /// </summary>
    private Task LoadUnprocessedSourcesAsync() => LoadAllAsync();

    /// <summary>
    /// 更新统计信息 —— 两个 Tab 的分类计数独立计算
    /// </summary>
    private void UpdateStatistics()
    {
        _unidentifiedCategoryCounts.Clear();
        foreach (var kv in _allUnprocessedSources
                     .GroupBy(s => s.PossibleTopCategory)
                     .ToDictionary(g => g.Key, g => g.Count()))
        {
            _unidentifiedCategoryCounts[kv.Key] = kv.Value;
        }

        _pendingCategoryCounts.Clear();
        foreach (var kv in _allPendingSources
                     .GroupBy(p => p.Source.PossibleTopCategory)
                     .ToDictionary(g => g.Key, g => g.Count()))
        {
            _pendingCategoryCounts[kv.Key] = kv.Value;
        }

        _totalCount = _allUnprocessedSources.Count;
        _pendingTotalCount = _allPendingSources.Count;
    }

    /// <summary>
    /// 获取当前 Tab 下某分类的计数（供统计卡片使用）
    /// </summary>
    private int GetCategoryCount(TopCategory category) =>
        CurrentCategoryCounts.GetValueOrDefault(category, 0);

    /// <summary>
    /// 应用筛选条件（两个 Tab 共享同一份筛选条件）
    /// </summary>
    private void ApplyFilters()
    {
        var unidentifiedQuery = _allUnprocessedSources.AsEnumerable();
        var pendingQuery = _allPendingSources.AsEnumerable();

        if (_categoryFilter.HasValue)
        {
            unidentifiedQuery = unidentifiedQuery.Where(s => s.PossibleTopCategory == _categoryFilter.Value);
            pendingQuery = pendingQuery.Where(p => p.Source.PossibleTopCategory == _categoryFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            unidentifiedQuery = unidentifiedQuery
                .Where(s => s.FullPath.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
            pendingQuery = pendingQuery
                .Where(p => p.Source.FullPath.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                            || p.Media.Title.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        _filteredSources = unidentifiedQuery.ToList();
        _filteredPendingSources = pendingQuery.ToList();
        StateHasChanged();
    }

    /// <summary>
    /// 设置类型筛选 —— 批量处理期间拒绝响应，避免筛选变动影响正在运行的批量任务
    /// </summary>
    private void SetCategoryFilter(TopCategory? category)
    {
        if (_isBatchProcessing)
            return;

        _categoryFilter = category;
        ApplyFilters();
    }

    /// <summary>
    /// 重置筛选条件 —— 批量处理期间拒绝响应
    /// </summary>
    private void ResetFilters()
    {
        if (_isBatchProcessing)
            return;

        _categoryFilter = null;
        _searchText = "";
        ApplyFilters();
    }

    /// <summary>
    /// Tab 切换处理 —— 清空选择（两个 Tab 的选择互不混用）并刷新 UI。
    /// 批量处理期间拒绝切换，避免选中项 / 筛选上下文错乱
    /// </summary>
    private void OnActiveTabChanged(int newIndex)
    {
        if (_isBatchProcessing)
            return;

        _activeTabIndex = newIndex;
        _selectedItems = new HashSet<MediaSource>();
        _selectedPendingItems = new HashSet<PendingEntry>();
        StateHasChanged();
    }

    /// <summary>
    /// 手动识别单个媒体源
    /// </summary>
    private async Task HandleIdentifyMediaAsync(MediaSource source)
    {
        var initOptions = Config.Identification.ToIdentificationOptions();
        initOptions.AutoAddToDatabase = false;

        var parameters = new DialogParameters
        {
            { "SourcePath", source.FullPath },
            { "InitialOptions", initOptions }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<IdentificationOptionsDialog>("识别选项配置", parameters, options);
        var result = await dialog.Result;

        if (result == null || result.Canceled)
            return;

        var identificationOptions = result.Data as Core.Models.Identification.IdentificationOptions;
        if (identificationOptions == null)
        {
            Snackbar.Add("获取识别选项失败", Severity.Error);
            return;
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        var progressReporter = new DialogProgressReporter();

        var loadingParameters = new DialogParameters
        {
            {
                "OnCancel", EventCallback.Factory.Create(this, () =>
                {
                    cancellationTokenSource.Cancel();
                    Snackbar.Add("已请求取消识别", Severity.Info);
                })
            }
        };

        var loadingOptions = new DialogOptions
        {
            NoHeader = true,
            CloseButton = false,
            CloseOnEscapeKey = false,
            Position = DialogPosition.Center
        };

        var loadingDialogRef =
            await DialogService.ShowAsync<IdentificationLoadingDialog>("", loadingParameters, loadingOptions);
        var dialogInstance = loadingDialogRef.Dialog as IdentificationLoadingDialog;

        progressReporter.OnProgress += (entry) =>
        {
            dialogInstance?.HandleProgress(entry);
        };

        try
        {
            var media = await FilesService.GetMediaByPath(source.FullPath, identificationOptions, progressReporter,
                cancellationTokenSource.Token);
            loadingDialogRef.Close();

            if (media != null)
            {
                await ShowMediaInfoDialogAsync(media);
                await LoadUnprocessedSourcesAsync();
            }
            else
            {
                Snackbar.Add($"无法识别: {source.FullPath}", Severity.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            loadingDialogRef.Close();
            Snackbar.Add("识别已取消", Severity.Info);
        }
        catch (Exception ex)
        {
            loadingDialogRef.Close();
            Log.Error(ex, "识别媒体失败: {Path}", source.FullPath);
            Snackbar.Add($"识别失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 【待识别 Tab】手动添加媒体 —— 跳过识别流程，让用户只填最少信息（标题 + TopCategory）
    /// 就创建一个空的 MediaBase 并直接入库，成功后跳转到 MediaPage 编辑模式补齐其他字段。
    /// 场景：识别源搜不到该作品（个人录制、冷门资源等）。
    /// </summary>
    private async Task HandleManualAddMediaAsync(MediaSource source)
    {
        var parameters = new DialogParameters
        {
            { nameof(ManualAddMediaDialog.Source), source }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<ManualAddMediaDialog>(
            "手动添加媒体", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: ManualAddMediaResult ok })
        {
            Snackbar.Add(
                ok.FullyFilled ? "已创建媒体" : "已创建媒体，即将进入编辑",
                Severity.Success);
            var suffix = ok.FullyFilled ? string.Empty : "?edit=true";
            NavigationManager.NavigateTo($"/media/{ok.MediaId}{suffix}");
        }
        // 跳转后本页卸载，无需 LoadAllAsync；取消则保持列表不变
    }

    /// <summary>
    /// 加入识别队列
    /// </summary>
    private async Task HandleAddToQueueAsync(MediaSource source)
    {
        try
        {
            var identificationOptions = Config.Identification.ToIdentificationOptions();
            identificationOptions.AutoAddToDatabase = true;

            var taskId = await FilesService.IdentifySingleMedia(source.FullPath, identificationOptions);
            var fileName = Path.GetFileName(source.FullPath);
            Snackbar.Add($"已将 {fileName} 加入识别队列，任务ID: {taskId}", Severity.Success);

            Log.Information("媒体识别任务已提交: {Path}, TaskId: {TaskId}", source.FullPath, taskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加入识别队列失败: {Path}", source.FullPath);
            Snackbar.Add($"加入识别队列失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 【待入库 Tab】预览识别结果 —— 弹 MediaInfoDialog。用户点"添加到数据库"即入库，点取消不丢弃。
    /// </summary>
    private async Task HandlePendingPreviewAsync(MediaBase media)
    {
        await ShowMediaInfoDialogAsync(media);
        await LoadAllAsync();
    }

    /// <summary>
    /// 【待入库 Tab】快捷入库 —— 不弹对话框直接调用 FilesService.AddMediaToDatabase。
    /// 入库期间该行的按钮会切换为 loading 态，同一行其他操作被禁用。
    /// </summary>
    private async Task HandlePendingCommitAsync(MediaSource source, MediaBase media)
    {
        // 已在进行中则直接忽略（防止快速双击造成并发入库）
        if (!_committingSourceIds.Add(source.Id))
            return;

        StateHasChanged();

        try
        {
            await FilesService.AddMediaToDatabase(media);
            Snackbar.Add($"已入库: {media.Title}", Severity.Success);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "入库失败: {Path}", source.FullPath);
            Snackbar.Add($"入库失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _committingSourceIds.Remove(source.Id);
            StateHasChanged();
        }
    }

    /// <summary>
    /// 【待入库 Tab】批量入库 —— 对选中的每条 PendingEntry 依次调 AddMediaToDatabase。
    /// 成功/失败数汇总后用 Snackbar 显示。入库期间按钮切为 loading 态，防止双击。
    /// </summary>
    private async Task HandleBatchCommitPendingAsync()
    {
        if (_selectedPendingItems.Count == 0 || _isBatchProcessing)
            return;

        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认批量入库",
            "把选中的识别结果一次性提交到媒体库。",
            intent: ConfirmIntent.Affirmative,
            confirmText: "入库",
            affectedCount: _selectedPendingItems.Count);

        if (!confirm)
            return;

        _isBatchProcessing = true;
        StateHasChanged();

        var itemsToCommit = _selectedPendingItems.ToList();
        var successCount = 0;
        var failCount = 0;

        foreach (var entry in itemsToCommit)
        {
            _committingSourceIds.Add(entry.Source.Id);
            StateHasChanged();

            try
            {
                await FilesService.AddMediaToDatabase(entry.Media);
                successCount++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量入库时失败: {Path}", entry.Source.FullPath);
                failCount++;
            }
            finally
            {
                _committingSourceIds.Remove(entry.Source.Id);
            }
        }

        _selectedPendingItems.Clear();
        _isBatchProcessing = false;

        Snackbar.Add($"批量入库完成：成功 {successCount} 条，失败 {failCount} 条",
            failCount > 0 ? Severity.Warning : Severity.Success);

        await LoadAllAsync();
    }

    /// <summary>
    /// 【待入库 Tab】批量丢弃识别结果 —— 对选中的每条 PendingEntry 删除 Pending 行并把源回到"待识别"。
    /// </summary>
    private async Task HandleBatchDiscardPendingAsync()
    {
        if (_selectedPendingItems.Count == 0 || _isBatchProcessing)
            return;

        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认批量丢弃",
            "丢弃后这些媒体源会回到\"待识别\"状态，识别结果被清除。",
            intent: ConfirmIntent.DestructiveBatch,
            confirmText: "丢弃",
            affectedCount: _selectedPendingItems.Count,
            warningLine: "丢弃不可恢复，但可重新识别");

        if (!confirm)
            return;

        _isBatchProcessing = true;
        StateHasChanged();

        var itemsToDiscard = _selectedPendingItems.ToList();
        var successCount = 0;
        var failCount = 0;

        foreach (var entry in itemsToDiscard)
        {
            try
            {
                await PendingIdentificationService.RemoveBySourceIdAsync(entry.Source.Id);

                var tracked = await DbContext.MediaSources.FirstOrDefaultAsync(m => m.Id == entry.Source.Id);
                if (tracked != null)
                {
                    tracked.Identified = false;
                    tracked.InDatabase = false;
                }

                successCount++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量丢弃时失败: {Path}", entry.Source.FullPath);
                failCount++;
            }
        }

        await DbContext.SaveChangesAsync();
        _selectedPendingItems.Clear();
        _isBatchProcessing = false;

        Snackbar.Add($"批量丢弃完成：成功 {successCount} 条，失败 {failCount} 条",
            failCount > 0 ? Severity.Warning : Severity.Success);

        await LoadAllAsync();
    }

    /// <summary>
    /// 【待入库 Tab】丢弃识别结果 —— 删除 PendingIdentification 并把 MediaSource 回到"待识别"状态。
    /// </summary>
    private async Task HandlePendingDiscardAsync(MediaSource source)
    {
        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认丢弃",
            "丢弃后该媒体源会回到\"待识别\"状态，识别结果被清除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "丢弃",
            targetName: Path.GetFileName(source.FullPath),
            targetIcon: Icons.Material.Filled.InsertDriveFile,
            warningLine: "丢弃不可恢复，但可重新识别");

        if (!confirm)
            return;

        try
        {
            await PendingIdentificationService.RemoveBySourceIdAsync(source.Id);

            var tracked = await DbContext.MediaSources.FirstOrDefaultAsync(m => m.Id == source.Id);
            if (tracked != null)
            {
                tracked.Identified = false;
                tracked.InDatabase = false;
                await DbContext.SaveChangesAsync();
            }

            Snackbar.Add("已丢弃识别结果", Severity.Info);
            await LoadAllAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "丢弃识别结果失败: {Path}", source.FullPath);
            Snackbar.Add($"丢弃失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 显示媒体信息对话框。对话框内部会处理入库操作，显示加载/错误状态，
    /// 成功则关闭并返回 Ok；本方法只在成功后显示成功提示。
    /// </summary>
    private async Task ShowMediaInfoDialogAsync(MediaBase media)
    {
        var parameters = new DialogParameters
        {
            { nameof(MediaInfoDialog.Media), media },
            {
                nameof(MediaInfoDialog.OnConfirmAsync),
                new Func<MediaBase, Task>(async m => await FilesService.AddMediaToDatabase(m))
            }
        };
        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<MediaInfoDialog>("媒体信息", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add($"添加媒体到数据库成功: {media.Title}", Severity.Success);
        }
    }

    /// <summary>
    /// 删除单个媒体源
    /// </summary>
    private async Task HandleDeleteSourceAsync(MediaSource source)
    {
        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认删除",
            "删除后该媒体源将从数据库中移除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: Path.GetFileName(source.FullPath),
            targetIcon: source.IsFolder
                ? Icons.Material.Filled.Folder
                : Icons.Material.Filled.InsertDriveFile);

        if (!confirm)
            return;

        try
        {
            if (source.MediaBase != null)
            {
                await MediaService.RemoveMediaAsync(source.MediaBase.Source!);
            }
            else
            {
                DbContext.MediaSources.Remove(source);
                await DbContext.SaveChangesAsync();
            }

            Snackbar.Add("删除成功", Severity.Success);
            await LoadUnprocessedSourcesAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除媒体源失败: {Path}", source.FullPath);
            Snackbar.Add($"删除失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打开文件位置
    /// </summary>
    private async Task HandleOpenLocationAsync(MediaSource source)
    {
        if (IsRemoteAccess)
        {
            Snackbar.Add("远程访问时无法打开本地文件位置", Severity.Warning);
            return;
        }

        try
        {
            var result = source.IsFolder
                ? await FileExplorerService.OpenFolderInExplorerAsync(source.FullPath)
                : await FileExplorerService.ShowFileInExplorerAsync(source.FullPath);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? "打开文件位置失败", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开文件位置失败: {Path}", source.FullPath);
            Snackbar.Add($"打开文件位置失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    private async Task HandleBatchDeleteAsync()
    {
        if (_selectedItems.Count == 0 || _isBatchProcessing)
            return;

        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认批量删除",
            "所有选中的媒体源记录将被永久删除。",
            intent: ConfirmIntent.DestructiveBatch,
            confirmText: "删除",
            affectedCount: _selectedItems.Count);

        if (!confirm)
            return;

        _isBatchProcessing = true;
        StateHasChanged();

        var itemsToDelete = _selectedItems.ToList();
        var successCount = 0;
        var failCount = 0;

        foreach (var source in itemsToDelete)
        {
            try
            {
                if (source.MediaBase != null)
                {
                    await MediaService.RemoveMediaAsync(source.MediaBase.Source!);
                }
                else
                {
                    DbContext.MediaSources.Remove(source);
                }

                successCount++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量删除时失败: {Path}", source.FullPath);
                failCount++;
            }
        }

        await DbContext.SaveChangesAsync();
        _selectedItems.Clear();
        _isBatchProcessing = false;

        Snackbar.Add($"批量删除完成：成功 {successCount} 个，失败 {failCount} 个",
            failCount > 0 ? Severity.Warning : Severity.Success);
        await LoadUnprocessedSourcesAsync();
    }

    /// <summary>
    /// 批量重新识别（加入队列）
    /// </summary>
    private async Task HandleBatchReidentifyAsync()
    {
        if (_selectedItems.Count == 0 || _isBatchProcessing)
            return;

        var confirm = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认批量识别",
            "将选中的媒体源加入识别队列，后台会依次处理。",
            intent: ConfirmIntent.Affirmative,
            confirmText: "加入队列",
            icon: Icons.Material.Filled.QueuePlayNext,
            affectedCount: _selectedItems.Count);

        if (!confirm)
            return;

        _isBatchProcessing = true;
        StateHasChanged();

        var itemsToProcess = _selectedItems.ToList();
        var successCount = 0;
        var failCount = 0;

        foreach (var source in itemsToProcess)
        {
            try
            {
                var identificationOptions = Config.Identification.ToIdentificationOptions();
                identificationOptions.AutoAddToDatabase = true;

                await FilesService.IdentifySingleMedia(source.FullPath, identificationOptions);
                successCount++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加入识别队列失败: {Path}", source.FullPath);
                failCount++;
            }
        }

        _selectedItems.Clear();
        _isBatchProcessing = false;
        StateHasChanged();

        Snackbar.Add($"已加入识别队列：成功 {successCount} 个，失败 {failCount} 个",
            failCount > 0 ? Severity.Warning : Severity.Success);
    }

    /// <summary>
    /// 获取类型图标（复用 MediaUIHelper，Unknown 特殊处理）
    /// </summary>
    private static string GetCategoryIcon(TopCategory category) => category switch
    {
        TopCategory.Unknown => Icons.Material.Filled.QuestionMark,
        _ => MediaUIHelper.GetCategoryIcon(category)
    };

    /// <summary>
    /// 获取类型显示名称
    /// </summary>
    private static string GetCategoryName(TopCategory category) =>
        CategoryNames.GetValueOrDefault(category, "未知");

    /// <summary>
    /// 获取类型颜色（复用 MediaUIHelper，Unknown 特殊处理）
    /// </summary>
    private static Color GetCategoryColor(TopCategory category) => category switch
    {
        TopCategory.Unknown => Color.Warning,
        _ => MediaUIHelper.GetMediaColor(category)
    };

    /// <summary>
    /// 获取目录路径（不包含文件名）
    /// </summary>
    private string GetDirectoryPath(string fullPath)
    {
        try
        {
            return Path.GetDirectoryName(fullPath) ?? fullPath;
        }
        catch
        {
            return fullPath;
        }
    }

    /// <summary>
    /// 获取统计卡片的 CSS 类（与 MediaOverviewPage 保持一致）。
    /// 批量处理期间追加 card-disabled 工具类：降低透明度 + pointer-events:none，
    /// 给出明确的禁用视觉反馈（键盘分支仍由 SetCategoryFilter 内的 guard 拦截）
    /// </summary>
    private string GetStatCardClass(TopCategory? category)
    {
        var isSelected = _categoryFilter == category;
        var baseClass = "pa-4 cursor-pointer card-stat";

        if (_isBatchProcessing)
            baseClass += " card-disabled";

        if (!isSelected)
            return baseClass;

        var colorSuffix = category.HasValue
            ? GetColorSuffix(GetCategoryColor(category.Value))
            : "primary";
        return $"{baseClass} card-bordered-{colorSuffix}";
    }

    private static string GetColorSuffix(Color color) => color switch
    {
        Color.Primary => "primary",
        Color.Secondary => "secondary",
        Color.Info => "info",
        Color.Success => "success",
        Color.Warning => "warning",
        Color.Error => "error",
        Color.Tertiary => "tertiary",
        _ => "default"
    };

    /// <summary>
    /// 批量按钮的 CSS 类：选中态（hasSelection=true）时加 glow 以吸引注意
    /// </summary>
    private static string GetBatchButtonClass(bool hasSelection)
        => hasSelection ? "mt-2 batch-btn batch-btn-active" : "mt-2 batch-btn";

    /// <summary>
    /// 格式化 Tab badge 数字：≤999 原样显示，≥1000 显示为 "999+"
    /// 避免极大数字撑破 tab 布局；aria-label 仍用真实数字
    /// </summary>
    private static string FormatCount(int count) => count > 999 ? "999+" : count.ToString();
}
