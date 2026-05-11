using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体库卡片网格里每张卡的轻量 VM。负责持有展示数据 + 异步加载封面图。
/// 由 <see cref="MediaOverviewViewModel"/> 在分页加载时按 MediaBase 实例构造。
/// </summary>
public partial class MediaCardViewModel : ObservableObject
{
    private readonly MediaBase _media;
    private readonly ImageCacheService _imageCache;

    [ObservableProperty]
    private Bitmap? _cover;

    public int Id => _media.Id;
    public string Title => _media.Title;
    public string? CircleName => _media.Circle?.Name;
    public float Rating => _media.Rating;
    public TopCategory TopCategory => _media.Category?.TopCategory ?? TopCategory.Unknown;

    /// <summary>顶级标签前 2 个，用作卡片底部 chip 行</summary>
    public IReadOnlyList<string> TopTags =>
        _media.Tags?.Take(2).Select(t => t.Name).ToList() ?? new List<string>();

    /// <summary>评分文本，星号 + 数字。Rating=0 时显示空字符串（卡片上不出现 "★ 0"）</summary>
    public string RatingText => Rating > 0 ? $"★ {Rating:F1}" : "";

    public bool HasRating => Rating > 0;

    /// <summary>分类色 brush，根据 TopCategory 动态映射</summary>
    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);

    /// <summary>分类图标，封面缺失时作为占位</summary>
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);

    public string CategoryDisplayName => TopCategoryStyles.DisplayName(TopCategory);

    public MediaCardViewModel(MediaBase media, ImageCacheService imageCache)
    {
        _media = media;
        _imageCache = imageCache;

        // 启动时立即异步加载封面（不阻塞构造，错误不向上抛）
        _ = LoadCoverAsync();
    }

    private async Task LoadCoverAsync()
    {
        var posterName = _media.Poster?.Name;
        if (string.IsNullOrWhiteSpace(posterName)) return;

        try
        {
            Cover = await _imageCache.GetOrLoadAsync(posterName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MediaCardViewModel 加载封面失败：MediaId={Id}", _media.Id);
        }
    }

    /// <summary>点击卡片时由 ItemsControl 触发。默认走主窗内嵌（NavigationService），
    /// 用户在详情页可点 [↗] 升级到独立窗。与 Web /media/{id} 体验对齐。</summary>
    [RelayCommand]
    private async Task OpenDetailAsync()
    {
        try
        {
            var nav = Program.Services?.GetService<NavigationService>();
            if (nav is null) return;
            await nav.NavigateToAsync<MediaDetailViewModel>(vm =>
            {
                vm.Mode = MediaDetailMode.EmbeddedPage;
                vm.RequestOpenDetail(Id);
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开媒体详情失败 Id={Id}", Id);
        }
    }
}
