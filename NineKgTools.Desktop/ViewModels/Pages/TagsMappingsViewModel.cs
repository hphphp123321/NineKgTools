using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

public enum TagMappingFilter { All, Active, Inactive, Unused }

/// <summary>
/// 标签映射管理 (§3.3)。最小可用：列表 + 启用切换 + 删除 + 搜索 / 状态过滤；
/// 编辑 dialog（修改源名 / 目标 / 优先级）留 P3。
/// </summary>
public partial class TagsMappingsViewModel : PageViewModelBase
{
    private readonly TagMappingService _mappingService;

    public override string Title => "标签映射";

    [ObservableProperty]
    private ObservableCollection<TagMappingItemViewModel> _items = new();

    /// <summary>所有映射的真源；过滤 / 搜索时不动它，只重建 Items 显示</summary>
    private List<TagMappingItemViewModel> _allItems = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsFilterActive))]
    [NotifyPropertyChangedFor(nameof(IsFilterInactive))]
    [NotifyPropertyChangedFor(nameof(IsFilterUnused))]
    private TagMappingFilter _selectedFilter = TagMappingFilter.All;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmpty = true;

    // 统计计数（顶部 chip 显示）
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _inactiveCount;

    [ObservableProperty]
    private int _totalHits;

    [ObservableProperty]
    private int _unusedCount;

    public bool IsFilterAll => SelectedFilter == TagMappingFilter.All;
    public bool IsFilterActive => SelectedFilter == TagMappingFilter.Active;
    public bool IsFilterInactive => SelectedFilter == TagMappingFilter.Inactive;
    public bool IsFilterUnused => SelectedFilter == TagMappingFilter.Unused;

    public TagsMappingsViewModel(TagMappingService mappingService)
    {
        _mappingService = mappingService;
    }

    public override Task OnEnterAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var mappings = await _mappingService.GetAllMappingsAsync(isActive: null, includeTargetTag: true);
            _allItems = mappings
                .OrderBy(m => m.Priority)
                .ThenBy(m => m.SourceName)
                .Select(m => new TagMappingItemViewModel(m, OnItemActiveToggled))
                .ToList();

            // 统计（基于全集）
            TotalCount = _allItems.Count;
            ActiveCount = _allItems.Count(i => i.IsActive);
            InactiveCount = _allItems.Count(i => !i.IsActive);
            TotalHits = _allItems.Sum(i => i.HitCount);
            UnusedCount = _allItems.Count(i => i.IsUnused);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TagsMappings 加载失败");
            _allItems = new();
            TotalCount = ActiveCount = InactiveCount = TotalHits = UnusedCount = 0;
            Items = new ObservableCollection<TagMappingItemViewModel>();
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void SelectFilter(string? filter)
    {
        if (string.IsNullOrEmpty(filter)) return;
        if (Enum.TryParse<TagMappingFilter>(filter, ignoreCase: true, out var f))
        {
            SelectedFilter = f;
            ApplyFilter();
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText?.Trim() ?? "";
        var filtered = _allItems.AsEnumerable();

        // 状态过滤
        filtered = SelectedFilter switch
        {
            TagMappingFilter.Active => filtered.Where(i => i.IsActive),
            TagMappingFilter.Inactive => filtered.Where(i => !i.IsActive),
            TagMappingFilter.Unused => filtered.Where(i => i.IsUnused),
            _ => filtered,
        };

        // 文本搜索（SourceName / TargetTagName / Description）
        if (q.Length > 0)
        {
            filtered = filtered.Where(i =>
                i.SourceName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (i.TargetTagName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (i.Description?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Items = new ObservableCollection<TagMappingItemViewModel>(filtered);
        ShowEmpty = Items.Count == 0;
    }

    /// <summary>由 TagMappingItemViewModel.IsActive 切换时回调，写回 db。</summary>
    private async void OnItemActiveToggled(TagMappingItemViewModel item, bool nowActive)
    {
        try
        {
            // 复制旧字段，仅改 IsActive，保证 UpdateMappingAsync 不会改其他属性
            var src = item.Source;
            var updated = new TagMapping
            {
                SourceName = src.SourceName,
                TargetTagId = src.TargetTagId,
                Description = src.Description,
                IsActive = nowActive,
                Priority = src.Priority,
            };
            await _mappingService.UpdateMappingAsync(item.Id, updated);

            // 同步本地源对象的状态，让全集统计准确
            src.IsActive = nowActive;

            // 重算统计
            ActiveCount = _allItems.Count(i => i.IsActive);
            InactiveCount = _allItems.Count(i => !i.IsActive);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "切换标签映射 IsActive 失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteMappingAsync(TagMappingItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除标签映射",
            message: $"将删除映射「{item.SourceName} → {item.TargetTagName}」。已建立的媒体-标签关联不受影响，但今后识别到「{item.SourceName}」时不再自动归到「{item.TargetTagName}」。",
            intent: DialogIntent.Destructive,
            targetName: item.SourceName,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _mappingService.DeleteMappingAsync(item.Id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除标签映射失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteUnusedAsync()
    {
        var unused = _allItems.Where(i => i.IsUnused).ToList();
        if (unused.Count == 0) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "清理未使用映射",
            message: $"将一次性删除 {unused.Count} 条 HitCount=0 的映射（从未匹配过任何媒体）。",
            intent: DialogIntent.DestructiveBatch,
            affectedCount: unused.Count,
            targetItems: unused.Take(20).Select(i => $"{i.SourceName} → {i.TargetTagName}").ToList(),
            confirmText: "确认清理");
        if (!confirmed) return;

        try
        {
            foreach (var item in unused)
            {
                await _mappingService.DeleteMappingAsync(item.Id);
            }
            Log.Information("批量清理未使用映射: {Count} 条", unused.Count);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量清理未使用映射失败");
        }
    }
}
