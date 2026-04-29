using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Components.Common;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] private MediaService MediaService { get; set; } = null!;
    [Inject] private FavoriteService FavoriteService { get; set; } = null!;
    [Inject] private IDbContextFactory<MediaDbContext> DbContextFactory { get; set; } = null!;

    // 统计数据
    private int _totalMediaCount = 0;
    private long _totalSize = 0;
    private Dictionary<TopCategory, int> _categoryCounts = new();
    private List<MediaBase> _recentMedia = new();
    private List<PhotoWallImageInfo> _randomImages = new();
    private int _totalCreatorCount = 0;
    private int _totalCircleCount = 0;
    private int _totalTagCount = 0;
    private int _totalFavoriteCount = 0;

    // 待处理入口：待识别和待入库数量，用于主页 inbox 导航卡片
    private int _unidentifiedCount;
    private int _pendingCount;
    private bool HasPendingItems => _unidentifiedCount > 0 || _pendingCount > 0;

    // 加载/错误状态
    private bool _isLoading = true;
    private bool _hasError;

    // 随机图片设置
    private int _randomImageCount = 20;

    // 取消令牌：导航离开时取消正在进行的 DB 查询，避免内存泄漏
    private readonly CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
        _isLoading = false;
    }

    /// <summary>
    /// 加载仪表板数据。每个方法使用独立 DbContext，可安全并行。
    /// </summary>
    private async Task LoadDashboardData()
    {
        _hasError = false;
        try
        {
            await Task.WhenAll(
                LoadTotalMediaCount(),
                LoadCategoryCounts(),
                LoadRecentMedia(),
                LoadRandomImages(),
                LoadGlobalStats(),
                LoadPendingCounts()
            );
        }
        catch (OperationCanceledException)
        {
            // 组件已销毁，静默忽略
        }
        catch (Exception ex)
        {
            _hasError = true;
            Log.Error(ex, "加载仪表板数据时出错");
        }
    }

    /// <summary>
    /// 加载总媒体数量和总大小
    /// </summary>
    private async Task LoadTotalMediaCount()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
        _totalMediaCount = await db.Medias.CountAsync(_cts.Token);
        _totalSize = await db.Medias.SumAsync(m => m.Size, _cts.Token);
    }

    /// <summary>
    /// 加载全局统计数据（创作者、社团、标签、收藏）
    /// </summary>
    private async Task LoadGlobalStats()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
        _totalCreatorCount = await db.Creators.CountAsync(_cts.Token);
        _totalCircleCount = await db.Circles.CountAsync(_cts.Token);
        _totalTagCount = await db.Tags.CountAsync(_cts.Token);
        _totalFavoriteCount = await db.Medias.CountAsync(m => m.Favorites.Any(), _cts.Token);
    }

    /// <summary>
    /// 加载"待处理"入口的两个计数：待识别（Identified = false）与待入库（PendingIdentification 行数）
    /// </summary>
    private async Task LoadPendingCounts()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
        _unidentifiedCount = await db.MediaSources.CountAsync(s => !s.Identified, _cts.Token);
        _pendingCount = await db.PendingIdentifications.CountAsync(_cts.Token);
    }

    /// <summary>
    /// 加载各分类的媒体数量
    /// </summary>
    private async Task LoadCategoryCounts()
    {
        await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
        var categoryGroups = await db.Medias
            .GroupBy(m => m.Category.TopCategory)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(_cts.Token);

        _categoryCounts.Clear();
        foreach (var group in categoryGroups)
        {
            _categoryCounts[group.Category] = group.Count;
        }
    }

    /// <summary>
    /// 加载最近入库的媒体（最多12个）
    /// </summary>
    private Task LoadRecentMedia()
    {
        var parameters = new MediaQueryParameters
        {
            PageSize = 12,
            SortOption = MediaSortOption.StoreDateDesc
        };

        var pagedResult = MediaService.GetPagedMediaList(parameters);
        _recentMedia = pagedResult.ToList();
        return Task.CompletedTask;
    }
    

    /// <summary>
    /// 加载随机图片（用于图片墙）。使用数据库层随机排序，避免全量 ID 载入内存。
    /// </summary>
    private async Task LoadRandomImages()
    {
        try
        {
            await using var db = await DbContextFactory.CreateDbContextAsync(_cts.Token);
            var imagesData = await db.Images
                .Where(i => i.Media != null)
                .OrderBy(x => EF.Functions.Random())
                .Take(_randomImageCount)
                .Select(i => new
                {
                    ImageId = i.Id,
                    ImageName = i.Name,
                    ImageWidth = i.Width,
                    ImageHeight = i.Height,
                    MediaId = i.Media != null ? i.Media.Id : (int?)null,
                    MediaTitle = i.Media != null ? i.Media.Title : "未知",
                    CategoryName = i.Media != null && i.Media.Category != null ? i.Media.Category.Name : "",
                    TopCategory = i.Media != null && i.Media.Category != null ? i.Media.Category.TopCategory : TopCategory.Unknown
                })
                .ToListAsync(_cts.Token);

            _randomImages = imagesData.Select(data => new PhotoWallImageInfo
            {
                ImageId = data.ImageId,
                ImageUrl = $"api/image/{data.ImageName}",
                ImageName = data.ImageName,
                MediaId = data.MediaId,
                MediaTitle = data.MediaTitle,
                CategoryName = data.CategoryName,
                CategoryColor = MediaUIHelper.GetMediaColor(data.TopCategory),
                Width = data.ImageWidth,
                Height = data.ImageHeight
            }).ToList();
        }
        catch (OperationCanceledException)
        {
            // 组件已销毁，静默忽略
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载随机图片时出错");
            _randomImages = new List<PhotoWallImageInfo>();
        }
    }

    /// <summary>
    /// 改变随机图片数量并重新加载
    /// </summary>
    private async Task ChangeRandomImageCount(int count)
    {
        _randomImageCount = count;
        await LoadRandomImages();
        StateHasChanged();
    }

    /// <summary>
    /// 获取分类图标
    /// </summary>
    private string GetCategoryIcon(TopCategory category)
    {
        return MediaUIHelper.GetCategoryIcon(category);
    }

    /// <summary>
    /// 获取分类名称
    /// </summary>
    private string GetCategoryName(TopCategory category)
    {
        return category switch
        {
            TopCategory.Video => "视频",
            TopCategory.Audio => "音频",
            TopCategory.Picture => "图片",
            TopCategory.Game => "游戏",
            TopCategory.Text => "文本",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取分类链接
    /// </summary>
    private string GetCategoryLink(TopCategory category)
    {
        return category switch
        {
            TopCategory.Video => "/media/overview/video",
            TopCategory.Audio => "/media/overview/audio",
            TopCategory.Picture => "/media/overview/picture",
            TopCategory.Game => "/media/overview/game",
            TopCategory.Text => "/media/overview/text",
            _ => "/media/overview"
        };
    }

    /// <summary>
    /// 刷新数据
    /// </summary>
    private async Task RefreshData()
    {
        _isLoading = true;
        StateHasChanged();

        await LoadDashboardData();

        _isLoading = false;
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        return FileSizeFormatter.FormatFileSize(bytes);
    }

    /// <summary>
    /// 待处理列的 CSS 类：数量为 0 时追加 muted 修饰类弱化视觉权重
    /// </summary>
    private static string GetPendingSlotClass(int count)
        => count > 0 ? "home-pending-slot" : "home-pending-slot home-pending-slot--muted";

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
