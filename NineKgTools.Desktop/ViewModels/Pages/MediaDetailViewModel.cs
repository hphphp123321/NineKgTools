using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体详情独立窗口的 VM。Phase 1.3 MVP 只读模式；编辑模式留给 Phase 2。
/// </summary>
public partial class MediaDetailViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;
    private readonly FilesService _filesService;
    private MediaBase? _media;

    [ObservableProperty]
    private string _windowTitle = "媒体详情";

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _loadError;

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string? _circleName;

    [ObservableProperty]
    private string _creatorsText = "";

    [ObservableProperty]
    private float _rating;

    [ObservableProperty]
    private string _ratingText = "";

    [ObservableProperty]
    private bool _hasRating;

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _aliasText = "";

    [ObservableProperty]
    private bool _hasAlias;

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private string _fileSize = "";

    [ObservableProperty]
    private string _releaseDateText = "";

    [ObservableProperty]
    private string _storeDateText = "";

    [ObservableProperty]
    private TopCategory _topCategory;

    [ObservableProperty]
    private string _categoryDisplayName = "";

    [ObservableProperty]
    private Bitmap? _cover;

    [ObservableProperty]
    private ObservableCollection<string> _tags = new();

    [ObservableProperty]
    private ObservableCollection<string> _favorites = new();

    [ObservableProperty]
    private bool _hasFavorites;

    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);

    public MediaDetailViewModel(MediaService mediaService, ImageCacheService imageCache, FilesService filesService)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        _filesService = filesService;
    }

    public async Task LoadAsync(int mediaId)
    {
        IsLoading = true;
        LoadError = null;

        try
        {
            var media = await _mediaService.GetMediaAsync(mediaId);
            if (media is null)
            {
                LoadError = "找不到该媒体。";
                return;
            }
            _media = media;
            ApplyToProperties(media);

            // 异步加载封面（不阻塞 UI 显示其他字段）
            if (media.Poster?.Name is { } posterName)
            {
                _ = LoadCoverAsync(posterName);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaDetailViewModel 加载失败：mediaId={Id}", mediaId);
            LoadError = "加载失败，请稍后重试。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCoverAsync(string posterName)
    {
        try
        {
            Cover = await _imageCache.GetOrLoadAsync(posterName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MediaDetailViewModel 加载封面失败：{Name}", posterName);
        }
    }

    private void ApplyToProperties(MediaBase media)
    {
        Title = media.Title;
        WindowTitle = media.Title;
        CircleName = media.Circle?.Name;
        TopCategory = media.Category?.TopCategory ?? TopCategory.Unknown;
        CategoryDisplayName = TopCategoryStyles.DisplayName(TopCategory);

        // 创作者列表（去重已通过 SyncCreators 维护）
        var creatorNames = media.Creators?.Select(c => c.Name).Distinct().ToList() ?? new List<string>();
        CreatorsText = string.Join(" · ", creatorNames);

        Rating = media.Rating;
        HasRating = Rating > 0;
        RatingText = HasRating ? $"★ {Rating:F1}" : "";

        Summary = string.IsNullOrWhiteSpace(media.Summary) ? "" : media.Summary;
        Description = string.IsNullOrWhiteSpace(media.Description) ? "" : media.Description;

        AliasText = media.AliasTitles?.Count > 0
            ? string.Join("、", media.AliasTitles)
            : "";
        HasAlias = !string.IsNullOrEmpty(AliasText);

        FilePath = media.Source?.FullPath ?? "";
        FileSize = FormatBytes(media.Size);

        ReleaseDateText = media.ReleaseDate?.ToString("yyyy-MM-dd") ?? "—";
        StoreDateText = media.StoreDate?.ToString("yyyy-MM-dd HH:mm") ?? "—";

        Tags = new ObservableCollection<string>(
            media.Tags?.Select(t => t.Name) ?? Enumerable.Empty<string>());

        Favorites = new ObservableCollection<string>(
            media.Favorites?.Select(f => f.Name) ?? Enumerable.Empty<string>());
        HasFavorites = Favorites.Count > 0;
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        if (string.IsNullOrEmpty(FilePath)) return;
        try
        {
            // Windows 资源管理器打开并选中
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{FilePath}\"");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "在文件管理器打开失败：{Path}", FilePath);
        }
    }

    [RelayCommand]
    private async Task ReidentifyAsync()
    {
        if (string.IsNullOrEmpty(FilePath)) return;
        try
        {
            await _filesService.IdentifySingleMedia(FilePath);
            Log.Information("已提交重新识别：{Path}", FilePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重新识别失败：{Path}", FilePath);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        return bytes switch
        {
            >= 1L << 30 => $"{bytes / (double)(1L << 30):F2} GB",
            >= 1L << 20 => $"{bytes / (double)(1L << 20):F2} MB",
            >= 1L << 10 => $"{bytes / (double)(1L << 10):F2} KB",
            _ => $"{bytes} B"
        };
    }
}
