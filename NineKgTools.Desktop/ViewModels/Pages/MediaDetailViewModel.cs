using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体详情独立窗口的 VM。Phase 1.3 起从只读升级为可编辑——IsEditMode 切换主操作按钮组与
/// 各字段控件（TextBlock vs TextBox / chip vs 选择器入口）。Save 走 MediaService.UpdateMediaAsync，
/// Delete 走 RemoveMediaAsync(id)，编辑过程中字段值是 ViewModel 的 draft；Cancel 时从 _media 恢复。
/// </summary>
public partial class MediaDetailViewModel : ObservableObject
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;
    private readonly FilesService _filesService;
    private readonly TagService _tagService;
    private readonly NineKgTools.Core.Services.Media.CreatorService _creatorService;
    private readonly FavoriteService _favoriteService;
    private MediaBase? _media;

    // ===== 编辑模式临时状态（draft）—— 进入编辑时初始化、Save/Cancel 时清空 =====
    private List<Tag> _editingTags = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingCreators = new();
    private List<Favorite> _editingFavorites = new();
    private Category? _editingCategory;

    /// <summary>别名 draft——直接绑给 EditableAliasList.Aliases，用户增删立即反映</summary>
    [ObservableProperty]
    private ObservableCollection<string> _editingAliases = new();

    /// <summary>删除成功后由 Window 订阅以关闭自身——VM 内部不持有 Window 引用。</summary>
    public event EventHandler? DeleteCompleted;

    [ObservableProperty]
    private string _windowTitle = "媒体详情";

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _loadError;

    /// <summary>编辑模式：true=各字段为可编辑控件 + 显示 Save/Cancel/Delete；false=显示 Edit/Reidentify/...</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFavoritesSection))]
    [NotifyPropertyChangedFor(nameof(ShowAliasSection))]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isSaving;

    /// <summary>保存失败时给用户显示的脱敏文案——详细 ex 已 Log</summary>
    [ObservableProperty]
    private string? _saveError;

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
    [NotifyPropertyChangedFor(nameof(ShowAliasSection))]
    private bool _hasAlias;

    /// <summary>编辑模式总显示别名区（让用户能添加首个别名）；只读模式仅当有别名时显示</summary>
    public bool ShowAliasSection => IsEditMode || HasAlias;

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
    private string _categorySubName = "";

    [ObservableProperty]
    private Bitmap? _cover;

    [ObservableProperty]
    private ObservableCollection<string> _tags = new();

    [ObservableProperty]
    private ObservableCollection<string> _favorites = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFavoritesSection))]
    private bool _hasFavorites;

    /// <summary>编辑模式下总显示（让"编辑"按钮可见）；只读模式下仅当有收藏夹时才显示</summary>
    public bool ShowFavoritesSection => IsEditMode || HasFavorites;

    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);

    public MediaDetailViewModel(
        MediaService mediaService,
        ImageCacheService imageCache,
        FilesService filesService,
        TagService tagService,
        NineKgTools.Core.Services.Media.CreatorService creatorService,
        FavoriteService favoriteService)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        _filesService = filesService;
        _tagService = tagService;
        _creatorService = creatorService;
        _favoriteService = favoriteService;
    }

    public async Task LoadAsync(int mediaId)
    {
        IsLoading = true;
        LoadError = null;
        IsEditMode = false;
        SaveError = null;

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
        CategorySubName = media.Category?.Name ?? "";

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

    // ========== 编辑模式：Edit / Cancel / Save / Delete ==========

    [RelayCommand]
    private void EnterEdit()
    {
        if (_media is null || IsEditMode) return;

        // draft：从 _media 拷一份引用列表（编辑过程中改 draft，不动 _media，方便 Cancel）
        _editingTags = _media.Tags?.ToList() ?? new List<Tag>();
        _editingCreators = _media.Creators?.ToList() ?? new List<NineKgTools.Core.Models.Media.Creator>();
        _editingFavorites = _media.Favorites?.ToList() ?? new List<Favorite>();
        _editingCategory = _media.Category;
        EditingAliases = new ObservableCollection<string>(_media.AliasTitles ?? new List<string>());
        SaveError = null;

        IsEditMode = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (!IsEditMode) return;
        _editingTags = new();
        _editingCreators = new();
        _editingFavorites = new();
        _editingCategory = null;
        EditingAliases = new ObservableCollection<string>();
        SaveError = null;

        // 字段从 _media 还原
        if (_media is not null) ApplyToProperties(_media);
        IsEditMode = false;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_media is null || !IsEditMode || IsSaving) return;
        IsSaving = true;
        SaveError = null;

        try
        {
            // 把 ViewModel draft 字段写回 tracked _media 实例——SaveChanges 会 detect 变化持久化
            _media.Title = string.IsNullOrWhiteSpace(Title) ? _media.Title : Title.Trim();
            _media.Summary = string.IsNullOrWhiteSpace(Summary) ? "暂无简介" : Summary.Trim();
            _media.Description = string.IsNullOrWhiteSpace(Description) ? "暂无描述" : Description.Trim();
            _media.Rating = Math.Clamp(Rating, 0f, 5f);

            if (_editingCategory is not null) _media.Category = _editingCategory;

            // 多对多关系用清空+加回保证 EF 正确 detect 变化
            _media.Tags.Clear();
            foreach (var t in _editingTags) _media.Tags.Add(t);

            _media.Creators.Clear();
            foreach (var c in _editingCreators) _media.Creators.Add(c);

            _media.Favorites.Clear();
            foreach (var f in _editingFavorites) _media.Favorites.Add(f);

            // 别名：List<string>，直接替换
            _media.AliasTitles = EditingAliases.ToList();

            await _mediaService.UpdateMediaAsync(_media);
            Log.Information("MediaDetail 保存成功：mediaId={Id}, title={Title}", _media.Id, _media.Title);

            // 重载（拿到 db 真实状态——比如 SyncCreators 后的 Creators 字段）
            await LoadAsync(_media.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaDetail 保存失败：mediaId={Id}", _media?.Id);
            SaveError = "保存失败，请稍后重试。";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_media is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            ownerVisual: null,
            title: "删除媒体",
            message: "删除后该媒体将从数据库永久移除（关联的标签 / 创作者 / 收藏夹关系一并清除）。源文件不会被删除。",
            intent: DialogIntent.Destructive,
            targetName: _media.Title,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _mediaService.RemoveMediaAsync(_media.Id);
            Log.Information("MediaDetail 已删除：mediaId={Id}", _media.Id);
            DeleteCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MediaDetail 删除失败：mediaId={Id}", _media.Id);
            SaveError = "删除失败，请稍后重试。";
        }
    }

    // ========== 编辑模式：触发各 dialog 修改 draft ==========

    [RelayCommand]
    private async Task EditTagsAsync()
    {
        if (!IsEditMode) return;
        try
        {
            var result = await TagSelectorDialog.ShowAsync(_editingTags, allowMultiSelect: true, _tagService);
            if (result is null) return;
            _editingTags = result;
            // 同步到 UI chip 列表
            Tags = new ObservableCollection<string>(_editingTags.Select(t => t.Name));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditTagsAsync 失败");
        }
    }

    [RelayCommand]
    private async Task EditCreatorsAsync()
    {
        if (!IsEditMode) return;
        try
        {
            var result = await CreatorSelectorDialog.ShowAsync(
                _editingCreators,
                allowMultiSelect: true,
                initialFilterType: null,
                _creatorService);
            if (result is null) return;
            _editingCreators = result;
            CreatorsText = string.Join(" · ", _editingCreators.Select(c => c.Name).Distinct());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditCreatorsAsync 失败");
        }
    }

    [RelayCommand]
    private async Task EditFavoritesAsync()
    {
        if (!IsEditMode) return;
        try
        {
            var result = await FavoriteSelectorDialog.ShowAsync(
                _editingFavorites,
                allowMultiSelect: true,
                _favoriteService);
            if (result is null) return;
            _editingFavorites = result;
            // 同步 UI 显示
            Favorites = new ObservableCollection<string>(_editingFavorites.Select(f => f.Name));
            HasFavorites = Favorites.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditFavoritesAsync 失败");
        }
    }

    [RelayCommand]
    private async Task EditCategoryAsync()
    {
        if (!IsEditMode) return;
        try
        {
            var initialCats = _editingCategory is not null
                ? new[] { _editingCategory }
                : Array.Empty<Category>();
            var result = await CategorySelectorDialog.ShowAsync(
                filterTopCategory: TopCategory.Unknown,
                initialSelected: initialCats,
                initialOnlyTop: false,
                initialSelectedTop: _editingCategory?.TopCategory);
            if (result is null) return;

            // 取第一个具体分类；若 OnlyTopCategory 则用 OtherX 兜底
            Category? picked = null;
            if (result.SelectedCategories.Count > 0)
            {
                picked = result.SelectedCategories[0];
            }
            else if (result.OnlyTopCategory && result.SelectedTopCategory != TopCategory.Unknown)
            {
                picked = result.SelectedTopCategory switch
                {
                    TopCategory.Video => StaticCategories.OtherVideo,
                    TopCategory.Audio => StaticCategories.OtherAudio,
                    TopCategory.Picture => StaticCategories.OtherPicture,
                    TopCategory.Text => StaticCategories.OtherText,
                    TopCategory.Game => StaticCategories.OtherGame,
                    _ => StaticCategories.Unknown,
                };
            }
            if (picked is null) return;

            _editingCategory = picked;
            // 同步 UI 显示（顶级 brush + 子分类名）
            TopCategory = picked.TopCategory;
            CategoryDisplayName = TopCategoryStyles.DisplayName(picked.TopCategory);
            CategorySubName = picked.Name;
            // CategoryBrush / CategoryIcon 是 computed 属性——TopCategory 改了它们也跟着变（ObservableObject 自动通知）
            OnPropertyChanged(nameof(CategoryBrush));
            OnPropertyChanged(nameof(CategoryIcon));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditCategoryAsync 失败");
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
