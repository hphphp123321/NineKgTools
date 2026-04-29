using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Tags;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using Serilog;

namespace NineKgTools.Pages.Tags;

public partial class TagsMappingsPage : ComponentBase
{
    [Inject]
    private TagMappingService TagMappingService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    // 数据
    private List<TagMapping> _allMappings = new();
    private List<TagMapping> _filteredMappings = new();
    private TagMappingStatistics _statistics = new();

    // 筛选条件
    private string _searchText = "";
    private bool? _statusFilter;

    // 选中项（批量操作）
    private HashSet<TagMapping> _selectedItems = new();

    // UI状态
    private bool _isLoading;

    private static readonly DialogOptions DefaultDialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseOnEscapeKey = true,
        CloseButton = true
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    /// <summary>
    /// 加载数据
    /// </summary>
    private async Task LoadDataAsync()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            // 并行加载映射列表和统计信息
            var mappingsTask = TagMappingService.GetAllMappingsAsync();
            var statisticsTask = TagMappingService.GetStatisticsAsync();

            await Task.WhenAll(mappingsTask, statisticsTask);

            _allMappings = await mappingsTask;
            _statistics = await statisticsTask;

            ApplyFilters();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载标签映射数据失败");
            Snackbar.Add($"加载失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 应用筛选条件
    /// </summary>
    private void ApplyFilters()
    {
        var query = _allMappings.AsEnumerable();

        // 状态筛选
        if (_statusFilter.HasValue)
        {
            query = query.Where(m => m.IsActive == _statusFilter.Value);
        }

        // 搜索筛选
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(m =>
                m.SourceName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                (m.TargetTag?.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (m.Description?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        _filteredMappings = query.ToList();
        StateHasChanged();
    }

    /// <summary>
    /// 设置状态筛选
    /// </summary>
    private void SetStatusFilter(bool? status)
    {
        _statusFilter = status;
        ApplyFilters();
    }

    /// <summary>
    /// 重置筛选
    /// </summary>
    private void ResetFilters()
    {
        _searchText = "";
        _statusFilter = null;
        ApplyFilters();
    }

    /// <summary>
    /// 添加映射
    /// </summary>
    private async Task AddMappingAsync()
    {
        var existingNames = _allMappings.Select(m => m.SourceName).ToList();
        var parameters = new DialogParameters<TagMappingEditorDialog>
        {
            { x => x.Mapping, null },
            { x => x.ExistingSourceNames, existingNames }
        };

        var dialog = await DialogService.ShowAsync<TagMappingEditorDialog>("添加映射", parameters, DefaultDialogOptions);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: TagMapping mapping })
        {
            try
            {
                await TagMappingService.AddMappingAsync(
                    mapping.SourceName,
                    mapping.TargetTagId!.Value,
                    mapping.Description);

                Snackbar.Add("添加映射成功", Severity.Success);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加映射失败");
                Snackbar.Add($"添加失败: {ex.Message}", Severity.Error);
            }
        }
    }

    /// <summary>
    /// 编辑映射
    /// </summary>
    private async Task EditMappingAsync(TagMapping mapping)
    {
        var existingNames = _allMappings.Select(m => m.SourceName).ToList();
        var parameters = new DialogParameters<TagMappingEditorDialog>
        {
            { x => x.Mapping, mapping },
            { x => x.ExistingSourceNames, existingNames }
        };

        var dialog = await DialogService.ShowAsync<TagMappingEditorDialog>("编辑映射", parameters, DefaultDialogOptions);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: TagMapping updatedMapping })
        {
            try
            {
                await TagMappingService.UpdateMappingAsync(mapping.Id, updatedMapping);
                Snackbar.Add("更新映射成功", Severity.Success);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "更新映射失败");
                Snackbar.Add($"更新失败: {ex.Message}", Severity.Error);
            }
        }
    }

    /// <summary>
    /// 删除映射
    /// </summary>
    private async Task DeleteMappingAsync(TagMapping mapping)
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "删除标签映射",
            "删除后该原始名不再自动映射到目标标签。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: mapping.SourceName,
            targetIcon: Icons.Material.Filled.SwapHoriz);

        if (confirmed)
        {
            try
            {
                await TagMappingService.DeleteMappingAsync(mapping.Id);
                Snackbar.Add("删除映射成功", Severity.Success);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除映射失败 MappingId={Id}", mapping.Id);
                Snackbar.Add("删除失败，请稍后重试。", Severity.Error);
            }
        }
    }

    /// <summary>
    /// 切换状态
    /// </summary>
    private async Task ToggleStatusAsync(TagMapping mapping)
    {
        try
        {
            await TagMappingService.ToggleMappingStatusAsync(mapping.Id);
            var statusText = mapping.IsActive ? "禁用" : "启用";
            Snackbar.Add($"已{statusText}映射", Severity.Success);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "切换状态失败");
            Snackbar.Add($"操作失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 批量启用
    /// </summary>
    private async Task BatchEnableAsync()
    {
        if (_selectedItems.Count == 0) return;

        try
        {
            var targets = _selectedItems.Where(m => !m.IsActive).ToList();
            await Task.WhenAll(targets.Select(m => TagMappingService.ToggleMappingStatusAsync(m.Id)));

            Snackbar.Add($"已启用 {targets.Count} 个映射", Severity.Success);
            _selectedItems.Clear();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量启用失败");
            Snackbar.Add($"操作失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 批量禁用
    /// </summary>
    private async Task BatchDisableAsync()
    {
        if (_selectedItems.Count == 0) return;

        try
        {
            var targets = _selectedItems.Where(m => m.IsActive).ToList();
            await Task.WhenAll(targets.Select(m => TagMappingService.ToggleMappingStatusAsync(m.Id)));

            Snackbar.Add($"已禁用 {targets.Count} 个映射", Severity.Success);
            _selectedItems.Clear();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量禁用失败");
            Snackbar.Add($"操作失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 批量删除
    /// </summary>
    private async Task BatchDeleteAsync()
    {
        if (_selectedItems.Count == 0) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "确认批量删除",
            "选中的映射全部被删除，对应的原始名不再自动映射到目标标签。",
            intent: ConfirmIntent.DestructiveBatch,
            confirmText: "删除",
            affectedCount: _selectedItems.Count);

        if (confirmed)
        {
            try
            {
                var targets = _selectedItems.ToList();
                await Task.WhenAll(targets.Select(m => TagMappingService.DeleteMappingAsync(m.Id)));

                Snackbar.Add($"已删除 {targets.Count} 个映射", Severity.Success);
                _selectedItems.Clear();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量删除失败");
                Snackbar.Add("操作失败，请稍后重试。", Severity.Error);
            }
        }
    }

    /// <summary>
    /// 清理未使用的映射
    /// </summary>
    private async Task CleanupUnusedAsync()
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "清理未使用映射",
            "将删除从未被使用过或超过 90 天未被使用的映射。",
            intent: ConfirmIntent.Destructive,
            confirmText: "清理",
            icon: Icons.Material.Filled.CleaningServices,
            warningLine: "清理后不可恢复");

        if (confirmed)
        {
            try
            {
                var count = await TagMappingService.CleanupUnusedMappingsAsync();
                Snackbar.Add($"已清理 {count} 个未使用的映射", Severity.Success);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清理未使用映射失败");
                Snackbar.Add("清理失败，请稍后重试。", Severity.Error);
            }
        }
    }
}
