using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.AI;
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
    private readonly OpenaiService _openaiService;
    private MediaBase? _media;

    // ===== 编辑模式临时状态（draft）—— 进入编辑时初始化、Save/Cancel 时清空 =====
    private List<Tag> _editingTags = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingCreators = new();
    private List<Favorite> _editingFavorites = new();
    private Category? _editingCategory;

    /// <summary>别名 draft——直接绑给 EditableAliasList.Aliases，用户增删立即反映</summary>
    [ObservableProperty]
    private ObservableCollection<string> _editingAliases = new();

    // ===== AI 翻译 draft（编辑模式才用） =====
    // Description 字段在桌面端 UI 已弃用（视觉用图片画廊取代，HTML 渲染抖动调不好），
    // 但 _media.Description / _media.DescriptionTranslated 数据库字段保留——SaveAsync 不再
    // 写这俩字段，避免破坏 Web 端编辑结果或识别源回填的内容。
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditingSummaryTranslated))]
    private string _editingSummaryTranslated = "";

    [ObservableProperty]
    private bool _isTranslatingSummary;

    public bool HasEditingSummaryTranslated => !string.IsNullOrWhiteSpace(EditingSummaryTranslated);

    /// <summary>发售日期 draft（DatePicker 用 DateTimeOffset?，Save 时转 DateTime?）</summary>
    [ObservableProperty]
    private DateTimeOffset? _editingReleaseDate;

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

    // ===== 图片画廊（Pictures slider，取代原 Description UI）=====
    /// <summary>媒体的 Pictures 集合 VM；ImageCacheService 异步加载每张 bitmap。
    /// 派生属性必须在这里同时 notify——SelectedPictureIndex 默认 0，LoadAsync 里 set 0→0
    /// 不会触发 PropertyChanged，binding 第一次 evaluate SelectedPicture 时 Pictures 还空，
    /// 此后 Pictures 替换没人通知 SelectedPicture 重算，结果第一张图初始显示空白。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPicture))]
    [NotifyPropertyChangedFor(nameof(PictureCounterText))]
    [NotifyPropertyChangedFor(nameof(HasPictures))]
    [NotifyPropertyChangedFor(nameof(HasMultiplePictures))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevPicture))]
    [NotifyPropertyChangedFor(nameof(CanGoNextPicture))]
    private ObservableCollection<MediaPictureItemViewModel> _pictures = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedPicture))]
    [NotifyPropertyChangedFor(nameof(PictureCounterText))]
    [NotifyPropertyChangedFor(nameof(CanGoPrevPicture))]
    [NotifyPropertyChangedFor(nameof(CanGoNextPicture))]
    private int _selectedPictureIndex;

    public MediaPictureItemViewModel? SelectedPicture =>
        Pictures.Count > 0 && SelectedPictureIndex >= 0 && SelectedPictureIndex < Pictures.Count
            ? Pictures[SelectedPictureIndex]
            : null;

    public string PictureCounterText =>
        Pictures.Count > 0 ? $"{SelectedPictureIndex + 1} / {Pictures.Count}" : "";

    public bool HasPictures => Pictures.Count > 0;
    public bool HasMultiplePictures => Pictures.Count > 1;
    public bool CanGoPrevPicture => SelectedPictureIndex > 0;
    public bool CanGoNextPicture => SelectedPictureIndex < Pictures.Count - 1;

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
        FavoriteService favoriteService,
        OpenaiService openaiService)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        _filesService = filesService;
        _tagService = tagService;
        _creatorService = creatorService;
        _favoriteService = favoriteService;
        _openaiService = openaiService;
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

        // 图片画廊：取代原 Description 区域。
        // 顺序：先 set SelectedPictureIndex=0（强制 notify 即使值未变，让 SelectedPicture
        // binding 第一次 evaluate 时已绑到对的 index），再 set Pictures（NotifyPropertyChangedFor
        // 链会重算 SelectedPicture 等所有派生属性）。
        var pictures = (media.Pictures ?? new List<Core.Models.Media.Image>())
            .Where(p => !string.IsNullOrEmpty(p.Name))
            .Select(p => new MediaPictureItemViewModel(p, _imageCache))
            .ToList();
        if (pictures.Count > 0) pictures[0].IsSelected = true;
        SelectedPictureIndex = 0;
        Pictures = new ObservableCollection<MediaPictureItemViewModel>(pictures);

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
        EditingSummaryTranslated = _media.SummaryTranslated ?? "";
        EditingReleaseDate = _media.ReleaseDate.HasValue
            ? new DateTimeOffset(_media.ReleaseDate.Value)
            : null;
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
        EditingSummaryTranslated = "";
        EditingReleaseDate = null;
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
            // _media.Description 不再写——桌面端已取消该字段 UI，避免覆盖识别源 / Web 端编辑结果
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

            // AI 翻译字段：写回 db（用户可能在编辑时点过翻译）
            _media.SummaryTranslated = string.IsNullOrWhiteSpace(EditingSummaryTranslated)
                ? null : EditingSummaryTranslated.Trim();
            // _media.DescriptionTranslated 不再写——同上，桌面端已不维护 description

            // 发售日期：DatePicker 返回 DateTimeOffset?，转 DateTime?
            _media.ReleaseDate = EditingReleaseDate?.DateTime;

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

    /// <summary>AI 翻译简介——返回中文。结果填到 EditingSummaryTranslated，Save 时写入 db。</summary>
    [RelayCommand]
    private async Task TranslateSummaryAsync()
    {
        if (!IsEditMode || IsTranslatingSummary || string.IsNullOrWhiteSpace(Summary)) return;
        IsTranslatingSummary = true;
        SaveError = null;
        try
        {
            var translated = await _openaiService.Translate(Summary, "中文");
            if (string.IsNullOrEmpty(translated))
            {
                SaveError = "翻译失败：可能未启用 AI 或返回空。请到设置启用 AI + 配置 OpenAI Key。";
                return;
            }
            EditingSummaryTranslated = translated;
            Log.Information("简介翻译完成 mediaId={Id}", _media?.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TranslateSummaryAsync 失败");
            SaveError = "翻译失败，请稍后重试。";
        }
        finally
        {
            IsTranslatingSummary = false;
        }
    }

    // ===== 图片画廊翻页命令 =====

    [RelayCommand]
    private void NextPicture()
    {
        if (CanGoNextPicture) SelectedPictureIndex++;
    }

    [RelayCommand]
    private void PrevPicture()
    {
        if (CanGoPrevPicture) SelectedPictureIndex--;
    }

    /// <summary>缩略图条点击切到指定图。CommandParameter 是 MediaPictureItemViewModel。</summary>
    [RelayCommand]
    private void SelectPicture(MediaPictureItemViewModel? item)
    {
        if (item is null) return;
        var idx = Pictures.IndexOf(item);
        if (idx >= 0) SelectedPictureIndex = idx;
    }

    /// <summary>SelectedPictureIndex 变化时维护各 thumb.IsSelected——缩略图选中态高亮用。</summary>
    partial void OnSelectedPictureIndexChanged(int value)
    {
        for (int i = 0; i < Pictures.Count; i++)
            Pictures[i].IsSelected = (i == value);
    }

    [RelayCommand]
    private async Task ChangePosterAsync()
    {
        if (!IsEditMode || _media is null) return;

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            Log.Warning("ChangePosterAsync: 无法获取 StorageProvider");
            return;
        }

        try
        {
            var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择封面图片",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp" },
                    },
                },
            });
            var file = picked.FirstOrDefault();
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            var bytes = await File.ReadAllBytesAsync(path);
            var ext = Path.GetExtension(path);
            var newName = $"poster_{Guid.NewGuid():N}{ext}";

            // 写回 tracked _media.Poster：同实例字段更新 → EF tracker 自动持久化 byte[]
            // _media.Poster=null 时新建（但不 attach 到 tracker，UpdateMediaAsync 路径下
            // 可能不会写入 db；识别入库流程一般会建 Poster placeholder，此路径少见）
            if (_media.Poster is null)
            {
                _media.Poster = new Image
                {
                    Name = newName,
                    Content = bytes,
                };
                Log.Warning("ChangePosterAsync: _media.Poster 原本为 null，新建 Image 实例（可能需要 ImageService 兜底持久化）");
            }
            else
            {
                _media.Poster.Name = newName;
                _media.Poster.Content = bytes;
                _media.Poster.Hash = null;        // 重新由后端 EnsureMediaImagesAsync 算
                _media.Poster.File = null;        // 旧 .cache 文件作废
                _media.Poster.Width = 0;
                _media.Poster.Height = 0;
            }

            // 立即更新 UI 预览
            using var ms = new MemoryStream(bytes);
            Cover = new Bitmap(ms);
            Log.Information("ChangePosterAsync: 已加载新封面 {Path} ({Size} bytes)", path, bytes.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChangePosterAsync 失败");
            SaveError = "封面更换失败，请稍后重试。";
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

    /// <summary>取主窗 TopLevel——给 OS 原生 picker 用。与 MediaOverviewViewModel.GetTopLevel 复制；放共享 service 里更干净。</summary>
    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is not null)
        {
            return Avalonia.Controls.TopLevel.GetTopLevel(lifetime.MainWindow);
        }
        return null;
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
