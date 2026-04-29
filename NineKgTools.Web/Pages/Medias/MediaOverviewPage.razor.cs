using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using NineKgTools.Components.FileExplorer;
using NineKgTools.Components.Medias;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Source;
using NineKgTools.Utils;

namespace NineKgTools.Pages.Medias;

public partial class MediaOverviewPage : ComponentBase
{
    [Inject] private MediaDbContext DbContext { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private SourceService SourceService { get; set; } = null!;
    [Inject] private MediaService MediaService { get; set; } = null!;

    [Parameter] public string? Category { get; set; }

    // 状态
    private TopCategory? _selectedCategory;
    private int _mediaShownViewKey = 0; // 用于强制刷新 MediaShownView
    private bool _isTransitioning = false; // 分类切换过渡动画状态
    private bool _isLoadingStats = true; // 统计数据加载状态

    // 统计数据
    private int _totalCount;
    private readonly Dictionary<TopCategory, int> _categoryCounts = new();

    // 分类卡片定义
    private static readonly TopCategory[] Categories =
    [
        TopCategory.Video, TopCategory.Audio, TopCategory.Picture,
        TopCategory.Text, TopCategory.Game
    ];

    private static readonly Dictionary<TopCategory, string> CategoryNames = new()
    {
        [TopCategory.Video] = "视频",
        [TopCategory.Audio] = "音频",
        [TopCategory.Picture] = "图片",
        [TopCategory.Text] = "文本",
        [TopCategory.Game] = "游戏"
    };

    private int GetCategoryCount(TopCategory category) =>
        _categoryCounts.GetValueOrDefault(category, 0);

    // 查询参数
    private MediaQueryParameters _queryParams = new()
    {
        SortOption = MediaSortOption.StoreDateDesc
    };

    // UI 属性
    private string _pageTitle => _selectedCategory.HasValue
        ? GetCategoryName(_selectedCategory.Value)
        : "媒体库";

    private string _mediaViewTitle => _selectedCategory.HasValue
        ? GetCategoryName(_selectedCategory.Value)
        : "全部媒体";

    private string _selectedIcon => _selectedCategory.HasValue
        ? MediaUIHelper.GetCategoryIcon(_selectedCategory.Value)
        : Icons.Material.Filled.Dashboard;

    private Color _selectedColor => _selectedCategory.HasValue
        ? MediaUIHelper.GetMediaColor(_selectedCategory.Value)
        : Color.Primary;

    protected override async Task OnInitializedAsync()
    {
        // 解析 URL 参数中的分类
        ParseCategoryFromUrl();
        await LoadStatistics();
    }

    protected override async Task OnParametersSetAsync()
    {
        // URL 参数变化时更新
        var oldCategory = _selectedCategory;
        ParseCategoryFromUrl();
        if (oldCategory != _selectedCategory)
        {
            UpdateQueryParams();
        }
    }

    private void ParseCategoryFromUrl()
    {
        if (string.IsNullOrEmpty(Category))
        {
            _selectedCategory = null;
            return;
        }

        _selectedCategory = Category.ToLower() switch
        {
            "video" or "videos" => TopCategory.Video,
            "audio" or "audios" => TopCategory.Audio,
            "picture" or "pictures" => TopCategory.Picture,
            "text" or "texts" => TopCategory.Text,
            "game" or "games" => TopCategory.Game,
            _ => null
        };
    }

    private async Task LoadStatistics()
    {
        _isLoadingStats = true;
        try
        {
            // 获取各分类统计
            var stats = await DbContext.Medias
                .GroupBy(m => m.Category.TopCategory)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .ToListAsync();

            _categoryCounts.Clear();
            foreach (var s in stats)
                _categoryCounts[s.Category] = s.Count;
            _totalCount = stats.Sum(s => s.Count);

            UpdateQueryParams();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "加载媒体统计数据失败");
            Snackbar.Add("加载媒体统计数据失败，请刷新重试", Severity.Error);
        }
        finally
        {
            _isLoadingStats = false;
        }
    }

    private async Task SelectCategory(TopCategory? category)
    {
        if (_selectedCategory == category) return;

        // 触发淡出动画
        _isTransitioning = true;
        StateHasChanged();

        // 等待淡出完成（需与 CSS transition duration 一致）
        await Task.Delay(160);

        _selectedCategory = category;
        _isTransitioning = false; // 先解除过渡状态，后续 StateHasChanged 会触发淡入
        UpdateQueryParams();

        // 更新 URL
        var url = category.HasValue
            ? $"/media/overview/{category.Value.ToString().ToLower()}"
            : "/media/overview";
        NavigationManager.NavigateTo(url, replace: true);
    }

    private void UpdateQueryParams()
    {
        _queryParams = new MediaQueryParameters
        {
            TopCategory = _selectedCategory,
            SortOption = MediaSortOption.StoreDateDesc
        };
        _mediaShownViewKey++; // 强制刷新 MediaShownView
        StateHasChanged();
    }

    private string GetCategoryCardClass(TopCategory? category)
    {
        var isSelected = _selectedCategory == category;
        var baseClass = "pa-4 cursor-pointer card-stat";

        if (isSelected)
        {
            var colorSuffix = category.HasValue
                ? GetColorSuffix(MediaUIHelper.GetMediaColor(category.Value))
                : "primary";
            return $"{baseClass} card-bordered-{colorSuffix}";
        }

        return baseClass;
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

    private static string GetCategoryName(TopCategory category) =>
        CategoryNames.GetValueOrDefault(category, "未知");

    /// <summary>
    /// 手动新建媒体 —— 两步流程：先让用户在 <see cref="MediaKindPickerDialog"/> 里可视化选择
    /// "文件夹 / 单文件"，再弹 FileExplorer 选路径，最后交给 ManualAddMediaHelper 走后续流程。
    /// </summary>
    private async Task HandleNewMediaAsync()
    {
        // 第 1 步：选择目标类别（文件夹 / 单文件）
        var kind = await MediaKindPickerDialog.ShowAsync(DialogService);
        if (kind is null) return; // 取消

        var selectMode = kind == MediaKind.Folder ? FileSelectMode.Folder : FileSelectMode.File;
        var title = kind == MediaKind.Folder ? "选择媒体文件夹" : "选择媒体文件";

        // 第 2 步：弹 FileExplorer 选路径
        var fileParams = new DialogParameters<FileExplorer>
        {
            { x => x.SelectFolderMode, selectMode },
            { x => x.AllowEdit, true },
        };
        var fileOptions = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
        };

        var fileDialog = await DialogService.ShowAsync<FileExplorer>(title, fileParams, fileOptions);
        var fileResult = await fileDialog.Result;

        if (fileResult is not { Canceled: false, Data: string selectedPath } || string.IsNullOrWhiteSpace(selectedPath))
            return;

        // 第 3 步：交给共享 Helper 处理重复检测、对话框、导航
        await ManualAddMediaHelper.OpenByPathAsync(
            selectedPath, DialogService, SourceService, MediaService, Snackbar, NavigationManager);
    }
}
