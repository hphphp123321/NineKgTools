using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// 媒体多选对话框 VM。对齐 Web 端 <c>MediaSelectorDialog</c>：
/// 搜索输入 → debounce 300ms → <see cref="MediaService.SearchMediaByTitleAsync"/>（maxResults=50, 排除自身）
/// → 卡片网格多选 → 确认返回选中 Id 列表给 caller。
///
/// 设计简化（vs Web）：
/// - 不做分类筛选 chip（搜索结果已最多 50，过滤反而干扰）
/// - 不做 list / grid 视图切换（grid 一种就够）
/// - 已选项跨搜索保留——切换搜索关键词后旧选还在（与 Web 同语义）
/// </summary>
public partial class MediaSelectorDialogContext : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;

    /// <summary>caller 传入的"自己"媒体 Id，搜索时强制排除——避免媒体跟自己关联</summary>
    public int? ExcludeMediaId { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowHint))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    [NotifyPropertyChangedFor(nameof(ShowHint))]
    private bool _isLoading;

    public ObservableCollection<MediaSelectorItemVm> Results { get; } = new();

    /// <summary>跨搜索保留的"已选"集合——切换搜索词后已选还在。展示用 Results 里每项的 IsSelected 切换</summary>
    private readonly HashSet<int> _selectedIds = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConfirmText))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private int _selectedCount;

    public bool HasSelection => SelectedCount > 0;

    /// <summary>0 选时显式标 "清空并确定"，避免用户以为啥也没干（与 Web GetConfirmLabel 一致）</summary>
    public string ConfirmText => SelectedCount == 0 ? "清空并确定" : $"确定（{SelectedCount} 项）";

    public bool ShowEmpty => !IsLoading && !string.IsNullOrWhiteSpace(SearchText) && Results.Count == 0;
    public bool ShowHint => !IsLoading && string.IsNullOrWhiteSpace(SearchText) && Results.Count == 0;

    private CancellationTokenSource? _searchCts;

    public MediaSelectorDialogContext(
        MediaService mediaService,
        ImageCacheService imageCache,
        int? excludeMediaId,
        System.Collections.Generic.IReadOnlyList<MediaBase> initialSelected)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        ExcludeMediaId = excludeMediaId;
        foreach (var m in initialSelected)
            _selectedIds.Add(m.Id);
        SelectedCount = _selectedIds.Count;
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = DebouncedSearchAsync(value);
    }

    /// <summary>debounce 300ms 触发实际查询；新输入会取消上一次未完成的查询</summary>
    private async Task DebouncedSearchAsync(string query)
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try { await Task.Delay(300, token); }
        catch (OperationCanceledException) { return; }

        if (token.IsCancellationRequested) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            Results.Clear();
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowHint));
            return;
        }

        IsLoading = true;
        try
        {
            var list = await _mediaService.SearchMediaByTitleAsync(query, 50, ExcludeMediaId);
            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var m in list)
            {
                Results.Add(MediaSelectorItemVm.From(m, _imageCache, _selectedIds.Contains(m.Id)));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaSelectorDialog 搜索失败 SearchTerm={Term}", query);
            Results.Clear();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleSelect(MediaSelectorItemVm? item)
    {
        if (item is null) return;
        if (_selectedIds.Remove(item.Id))
        {
            item.IsSelected = false;
        }
        else
        {
            _selectedIds.Add(item.Id);
            item.IsSelected = true;
        }
        SelectedCount = _selectedIds.Count;
    }

    /// <summary>caller 在 Confirm 后调用拿到选中的 Id 列表。空列表 = 用户清空所有关联</summary>
    public System.Collections.Generic.List<int> GetSelectedIds() => _selectedIds.ToList();
}

/// <summary>结果列表里每项的 VM：Id / Title / 分类 + 异步加载封面 Bitmap</summary>
public partial class MediaSelectorItemVm : ObservableObject
{
    public int Id { get; }
    public string Title { get; }
    public string? CategoryName { get; }
    public bool HasCategory => !string.IsNullOrEmpty(CategoryName);

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private Bitmap? _poster;
    [ObservableProperty] private bool _hasPoster;

    private MediaSelectorItemVm(int id, string title, string? cat, bool selected)
    {
        Id = id;
        Title = title;
        CategoryName = cat;
        IsSelected = selected;
    }

    public static MediaSelectorItemVm From(MediaBase m, ImageCacheService imageCache, bool selected)
    {
        var vm = new MediaSelectorItemVm(m.Id, m.Title, m.Category?.Name, selected);
        var posterName = m.Poster?.Name;
        if (!string.IsNullOrEmpty(posterName))
            _ = LoadPosterAsync(vm, imageCache, posterName);
        return vm;
    }

    private static async Task LoadPosterAsync(MediaSelectorItemVm vm, ImageCacheService imageCache, string name)
    {
        try
        {
            var bmp = await imageCache.GetOrLoadAsync(name);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.Poster = bmp;
                vm.HasPoster = bmp != null;
            });
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MediaSelectorItemVm 封面加载失败 Name={Name}", name);
        }
    }
}
