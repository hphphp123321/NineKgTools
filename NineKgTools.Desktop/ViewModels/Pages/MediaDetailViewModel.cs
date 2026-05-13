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
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 媒体详情 VM。Phase 1.3 起从只读升级为可编辑——IsEditMode 切换主操作按钮组与
/// 各字段控件（TextBlock vs TextBox / chip vs 选择器入口）。Save 走 MediaService.UpdateMediaAsync，
/// Delete 走 RemoveMediaAsync(id)，编辑过程中字段值是 ViewModel 的 draft；Cancel 时从 _media 恢复。
///
/// **双模式 host**（v2 升级）：
/// - <see cref="MediaDetailMode.EmbeddedPage"/>：主窗内嵌页（默认，与 Web /media/{id} 体验一致）
/// - <see cref="MediaDetailMode.IndependentWindow"/>：独立窗口（power user 选项，点 [↗] 弹出）
/// 同一份 UI（<c>Views/Components/MediaDetailContent.axaml</c>）+ 不同 host（<c>Views/Pages/MediaDetailPage</c> / <c>Views/Windows/MediaDetailWindow</c>）；
/// 按 Mode 切换图钉 vs nav bar 可见性、OpenRelatedMedia 切换分支等。
/// </summary>
public partial class MediaDetailViewModel : NineKgTools.Desktop.ViewModels.PageViewModelBase
{
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;
    private readonly ImageService _imageService;
    private readonly FilesService _filesService;
    private readonly TagService _tagService;
    private readonly NineKgTools.Core.Services.Media.CreatorService _creatorService;
    private readonly FavoriteService _favoriteService;
    private readonly OpenaiService _openaiService;
    private readonly NavigationService _navigationService;
    private readonly IdentificationFlowService _identificationFlow;
    private readonly IDbContextFactory<MediaDbContext> _dbFactory;
    private readonly WindowManager _windowManager;
    private readonly DesktopPreferences _preferences;
    private MediaBase? _media;

    // ===== 编辑模式临时状态（draft）—— 进入编辑时初始化、Save/Cancel 时清空 =====
    private List<Tag> _editingTags = new();
    private List<Favorite> _editingFavorites = new();
    private Category? _editingCategory;

    /// <summary>
    /// 图片 draft 列表——增删图片只动这个，Save 时 diff 旧 _media.Pictures 算出
    /// 增量（新增→AddOrFindImagesAsync 入库；移除→RemoveImageAsync 删 db + 文件）。
    /// 元素来自两类来源：1) EnterEdit 时从 _media.Pictures 拷贝（已入库实体）
    /// 2) AddPicture 命令新建（id=0、带 Content byte[]）。
    /// </summary>
    private List<Core.Models.Media.Image> _editingPictures = new();

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

    /// <summary>当前 VM 跑在哪种 host 里——决定图钉 / nav bar 可见性 / 关联媒体跳转行为</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmbeddedPage))]
    [NotifyPropertyChangedFor(nameof(IsIndependentWindow))]
    private MediaDetailMode _mode = MediaDetailMode.EmbeddedPage;

    public bool IsEmbeddedPage => Mode == MediaDetailMode.EmbeddedPage;
    public bool IsIndependentWindow => Mode == MediaDetailMode.IndependentWindow;

    /// <summary>独立窗 Topmost 双向同步——MediaDetailWindow code-behind 监听该属性 ↔ Window.Topmost。
    /// in-page 模式图钉 IsVisible=false，此属性不会被改</summary>
    [ObservableProperty]
    private bool _isTopmost;

    /// <summary>历史栈是否非空——in-page nav bar 的 [← 返回] 按钮 IsEnabled 绑此。
    /// 在 OnEnterAsync 订阅 NavigationService.CanGoBackChanged 同步；OnLeaveAsync 取消订阅</summary>
    [ObservableProperty]
    private bool _canGoBack;

    /// <summary>NavigationService 在 configureBeforeEnter 阶段调 RequestOpenDetail 写入；
    /// OnEnterAsync 读它触发 LoadAsync——与 Tags/Creators/Circles 同款"延迟读取"模式</summary>
    private int? _pendingMediaId;

    /// <summary>NavigationService 导航前调用：configureBeforeEnter 是 sync 不能 await，
    /// 写字段让 OnEnterAsync 异步消费</summary>
    public void RequestOpenDetail(int mediaId)
    {
        _pendingMediaId = mediaId > 0 ? mediaId : null;
    }

    [ObservableProperty]
    private bool _isLoading = true;

    [ObservableProperty]
    private string? _loadError;

    /// <summary>编辑模式：true=各字段为可编辑控件 + 显示 Save/Cancel/Delete；false=显示 Edit/Reidentify/...
    /// 注意：收藏夹不受此 mode 控制——任何时候都能点 "编辑" 改收藏夹（非编辑态立即 commit；编辑态走 draft）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAliasSection))]
    [NotifyPropertyChangedFor(nameof(ShowPicturesSection))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPicturesHint))]
    [NotifyPropertyChangedFor(nameof(ShowDirectorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowVoiceActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowScreenWritersSection))]
    [NotifyPropertyChangedFor(nameof(ShowIllustratorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowMusiciansSection))]
    [NotifyPropertyChangedFor(nameof(ShowAuthorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowRelatedMediasSection))]
    [NotifyPropertyChangedFor(nameof(ShowAddCirclePlaceholder))]
    [NotifyPropertyChangedFor(nameof(CircleChipTooltip))]
    private bool _isEditMode;

    [ObservableProperty]
    private bool _isSaving;

    /// <summary>保存失败时给用户显示的脱敏文案——详细 ex 已 Log</summary>
    [ObservableProperty]
    private string? _saveError;

    /// <summary>媒体标题（绑 Hero 区 TextBlock/TextBox + WindowTitle 同步源）。
    /// 注意：字段名 _mediaTitle 而非 _title—— PageViewModelBase 已有 abstract <see cref="Title"/>，
    /// 同名 ObservableProperty 会冲突。XAML 改绑 <c>{Binding MediaTitle}</c>。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string _mediaTitle = "";

    /// <summary>PageViewModelBase.Title override —— 显示在 NavigationView header / 历史栈等框架位置。
    /// MediaTitle 空时给"媒体详情"占位；有标题时拼"媒体详情 — {title}"</summary>
    public override string Title => string.IsNullOrWhiteSpace(MediaTitle)
        ? "媒体详情"
        : $"媒体详情 — {MediaTitle}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCircle))]
    [NotifyPropertyChangedFor(nameof(ShowAddCirclePlaceholder))]
    private string? _circleName;

    public bool HasCircle => !string.IsNullOrEmpty(CircleName);

    /// <summary>编辑模式下且当前无社团时，显示"+ 添加社团"占位 chip 让用户能补全。
    /// 浏览模式下永远不显示（社团缺失就缺失，与用户当下浏览动作无关）。</summary>
    public bool ShowAddCirclePlaceholder => IsEditMode && !HasCircle;

    /// <summary>社团 chip 的 tooltip：浏览态提示跳页，编辑态提示换社团。
    /// 与 CircleChipClickCommand 行为分支保持语义一致。</summary>
    public string CircleChipTooltip => IsEditMode
        ? "点击选择其他社团（创建 / 编辑社团请去「社团」页面）"
        : "查看该社团关联媒体";

    // ===== 创作者按职责分组（替代原 CreatorsText 单字符串扁平化设计）==========================
    // ApplyToProperties 按 _media is VideoMedia/AudioMedia/... 分发数据到对应字段；
    // 各 TopCategory 用到的字段子集（与 Web 端 EditableCreatorList 一致）：
    //   Video    : Directors / ScreenWriters / Illustrators / Actors / Musicians / Makers (List<Circle>)
    //   Audio    : VoiceActors / ScreenWriters / Illustrators / Musicians / Authors
    //   Game     : ScreenWriters / Illustrators / VoiceActors / Musicians / Authors
    //   Picture  : Illustrators / Actors / Authors
    //   Text     : Illustrators / Author (单个 → Authors 列表里至多 1 项)
    // 每个 ObservableCollection<string> 对应一个右侧栏 section 的 chip 列表数据源；
    // Has* 派生 bool 控制对应 section 的可见性，HasAnyCreators 控制整个"创作者"分组 header。

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDirectors))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowDirectorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _directors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoiceActors))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowVoiceActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _voiceActors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScreenWriters))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowScreenWritersSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _screenWriters = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIllustrators))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowIllustratorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _illustrators = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActors))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _actors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMusicians))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowMusiciansSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _musicians = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAuthors))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowAuthorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _authors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMakers))]
    [NotifyPropertyChangedFor(nameof(HasAnyCreators))]
    [NotifyPropertyChangedFor(nameof(ShowMakersSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private ObservableCollection<string> _makers = new();

    public bool HasDirectors => Directors.Count > 0;
    public bool HasVoiceActors => VoiceActors.Count > 0;
    public bool HasScreenWriters => ScreenWriters.Count > 0;
    public bool HasIllustrators => Illustrators.Count > 0;
    public bool HasActors => Actors.Count > 0;
    public bool HasMusicians => Musicians.Count > 0;
    public bool HasAuthors => Authors.Count > 0;
    public bool HasMakers => Makers.Count > 0;
    public bool HasAnyCreators =>
        HasDirectors || HasVoiceActors || HasScreenWriters || HasIllustrators ||
        HasActors || HasMusicians || HasAuthors || HasMakers;

    // === TopCategory 的职责支持矩阵（与 Web EditableCreatorList 行为一致）===
    // Supports{X} 用 TopCategory 决定该类型支不支持某职责。
    // Show{X}Section 进一步用 IsEditMode 控制：编辑模式即使空也显示让用户能添加；只读时仅有数据才显示。
    public bool SupportsDirectors => TopCategory == TopCategory.Video;
    public bool SupportsVoiceActors => TopCategory is TopCategory.Audio or TopCategory.Game;
    public bool SupportsScreenWriters => TopCategory is TopCategory.Video or TopCategory.Audio or TopCategory.Game;
    public bool SupportsIllustrators => TopCategory is TopCategory.Video or TopCategory.Audio or TopCategory.Game or TopCategory.Picture or TopCategory.Text;
    public bool SupportsActors => TopCategory is TopCategory.Video or TopCategory.Picture;
    public bool SupportsMusicians => TopCategory is TopCategory.Video or TopCategory.Audio or TopCategory.Game;
    public bool SupportsAuthors => TopCategory is TopCategory.Audio or TopCategory.Game or TopCategory.Picture or TopCategory.Text;
    public bool SupportsMakers => TopCategory == TopCategory.Video;

    public bool ShowDirectorsSection => SupportsDirectors && (HasDirectors || IsEditMode);
    public bool ShowVoiceActorsSection => SupportsVoiceActors && (HasVoiceActors || IsEditMode);
    public bool ShowScreenWritersSection => SupportsScreenWriters && (HasScreenWriters || IsEditMode);
    public bool ShowIllustratorsSection => SupportsIllustrators && (HasIllustrators || IsEditMode);
    public bool ShowActorsSection => SupportsActors && (HasActors || IsEditMode);
    public bool ShowMusiciansSection => SupportsMusicians && (HasMusicians || IsEditMode);
    public bool ShowAuthorsSection => SupportsAuthors && (HasAuthors || IsEditMode);
    public bool ShowMakersSection => SupportsMakers && HasMakers; // Makers 暂只读——编辑模式不开放
    public bool ShowAnyCreatorsSection =>
        ShowDirectorsSection || ShowVoiceActorsSection || ShowScreenWritersSection ||
        ShowIllustratorsSection || ShowActorsSection || ShowMusiciansSection ||
        ShowAuthorsSection || ShowMakersSection;

    // 各职责的 draft（编辑时改它，Save 时按 _media 实际类型写回）
    private List<NineKgTools.Core.Models.Media.Creator> _editingDirectors = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingVoiceActors = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingScreenWriters = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingIllustrators = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingActors = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingMusicians = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingAuthors = new();
    private List<NineKgTools.Core.Models.Media.Creator> _editingMakers = new(); // Makers 实际是 List<Circle>，但用 Creator 对话框也能复用——见 SaveAsync

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RatingStar1))]
    [NotifyPropertyChangedFor(nameof(RatingStar2))]
    [NotifyPropertyChangedFor(nameof(RatingStar3))]
    [NotifyPropertyChangedFor(nameof(RatingStar4))]
    [NotifyPropertyChangedFor(nameof(RatingStar5))]
    private float _rating;

    [ObservableProperty]
    private string _ratingText = "";

    [ObservableProperty]
    private bool _hasRating;

    /// <summary>5 颗星的 Geometry 派生：每颗按 Rating >= idx 决定 filled / outline。
    /// 由 Rating 字段 NotifyPropertyChangedFor 链触发——切 Rating 时自动重新解析图标。</summary>
    public Geometry? RatingStar1 => StarGeometryFor(1);
    public Geometry? RatingStar2 => StarGeometryFor(2);
    public Geometry? RatingStar3 => StarGeometryFor(3);
    public Geometry? RatingStar4 => StarGeometryFor(4);
    public Geometry? RatingStar5 => StarGeometryFor(5);

    private Geometry? StarGeometryFor(int idx)
    {
        var key = (int)Math.Round(Rating) >= idx ? "IconStarFilled" : "IconStarOutline";
        if (Avalonia.Application.Current?.Resources.TryGetResource(
                key, Avalonia.Application.Current.ActualThemeVariant, out var obj) == true
            && obj is Geometry g)
        {
            return g;
        }
        return null;
    }

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
    [NotifyPropertyChangedFor(nameof(ShowPicturesSection))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyPicturesHint))]
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

    /// <summary>编辑模式下永远显示 Pictures section（让 Add 按钮可见）；只读模式仅当有图时显示。</summary>
    public bool ShowPicturesSection => IsEditMode || HasPictures;

    /// <summary>编辑模式 + 当前没图 → 显示空状态卡（提示用户去点"添加图片"）。</summary>
    public bool ShowEmptyPicturesHint => IsEditMode && !HasPictures;

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

    /// <summary>当前入口文件绝对路径（来自 MediaSource.EntryFilePath）。null/空串表示未设置。
    /// 未设置 → Hero 主操作组显示 [🎯 设置入口] 单按钮；已设置 → 显示 [▶ 打开] + [⚙] 两按钮
    /// （齿轮继续走 SetEntryFileCommand 改入口）。所有类型的媒体都参与——单文件媒体 MediaSource
    /// 构造时已自动 EntryFilePath=FullPath，所以一进来就是"已设置"状态，UI 直接显示打开+齿轮。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEntryFile))]
    [NotifyPropertyChangedFor(nameof(EntryFileName))]
    private string? _entryFilePath;

    public bool HasEntryFile => !string.IsNullOrEmpty(EntryFilePath);
    public string EntryFileName => string.IsNullOrEmpty(EntryFilePath)
        ? string.Empty
        : System.IO.Path.GetFileName(EntryFilePath);

    [ObservableProperty]
    private string _releaseDateText = "";

    [ObservableProperty]
    private string _storeDateText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryBrush))]
    [NotifyPropertyChangedFor(nameof(CategoryFillBrush))]
    [NotifyPropertyChangedFor(nameof(CategoryIcon))]
    [NotifyPropertyChangedFor(nameof(SupportsDirectors))]
    [NotifyPropertyChangedFor(nameof(SupportsVoiceActors))]
    [NotifyPropertyChangedFor(nameof(SupportsScreenWriters))]
    [NotifyPropertyChangedFor(nameof(SupportsIllustrators))]
    [NotifyPropertyChangedFor(nameof(SupportsActors))]
    [NotifyPropertyChangedFor(nameof(SupportsMusicians))]
    [NotifyPropertyChangedFor(nameof(SupportsAuthors))]
    [NotifyPropertyChangedFor(nameof(SupportsMakers))]
    [NotifyPropertyChangedFor(nameof(ShowDirectorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowVoiceActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowScreenWritersSection))]
    [NotifyPropertyChangedFor(nameof(ShowIllustratorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowActorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowMusiciansSection))]
    [NotifyPropertyChangedFor(nameof(ShowAuthorsSection))]
    [NotifyPropertyChangedFor(nameof(ShowMakersSection))]
    [NotifyPropertyChangedFor(nameof(ShowAnyCreatorsSection))]
    private TopCategory _topCategory;

    [ObservableProperty]
    private string _categoryDisplayName = "";

    [ObservableProperty]
    private string _categorySubName = "";

    [ObservableProperty]
    private Bitmap? _cover;

    /// <summary>"封面玻璃材质"背景图——预渲染好的 60px 模糊 + 缩到 400x600 的 Bitmap。
    /// 仅当 <see cref="UseGlassBackground"/> 开启且 Cover 加载成功后才异步生成。
    /// 切换媒体 / 切关闭 backdrop 时会清空。AXAML 用 <see cref="HasGlassBackdrop"/> 控制 IsVisible。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGlassBackdrop))]
    private Bitmap? _blurredPoster;

    /// <summary>用户设置——是否启用封面玻璃背景。由 <see cref="DesktopPreferences"/> 持久化，
    /// VM 订阅 UseGlassBackgroundChanged 实时同步该字段，UI binding 此值切换 backdrop 层 IsVisible。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGlassBackdrop))]
    private bool _useGlassBackground;

    /// <summary>UI Z-stack backdrop 是否显示 = 偏好开启 && 模糊图加载完成。
    /// 模糊图加载中 / 没有封面 → 等同 fallback 到原 Mica（什么也不画）。</summary>
    public bool HasGlassBackdrop => UseGlassBackground && BlurredPoster != null;

    [ObservableProperty]
    private ObservableCollection<string> _tags = new();

    /// <summary>收藏夹 pill 列表——每条 = name + hash 派生的渐变 brush（FavoriteGradientHelper 缓存）。
    /// Hero 区评分下方的彩色 pill 行就绑这个；HasFavorites 控制空状态卡（虚线 + CTA）显隐。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFavorites))]
    private ObservableCollection<FavoritePillViewModel> _favoritePills = new();

    public bool HasFavorites => FavoritePills.Count > 0;

    /// <summary>"相关媒体"section 卡片集合——一对一映射 _media.RelatedMedias。
    /// 与 Web 端 RelatedMedias List 等价。即时持久化模式（与 Web 一致）：
    /// 不进 EnterEdit/Cancel draft 流程，添加 / 删除均直接调 MediaService 写库，
    /// 因为双向关联涉及对方媒体的数据，纯前端 draft 无法表达完整语义。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRelatedMedias))]
    [NotifyPropertyChangedFor(nameof(ShowRelatedMediasSection))]
    private ObservableCollection<RelatedMediaItemViewModel> _relatedMedias = new();

    public bool HasRelatedMedias => RelatedMedias.Count > 0;

    /// <summary>section 是否显示：有数据 OR 编辑模式（让用户能添加首条）</summary>
    public bool ShowRelatedMediasSection => HasRelatedMedias || IsEditMode;

    public IBrush? CategoryBrush => TopCategoryStyles.ResolveAccentBrush(TopCategory);
    public IBrush? CategoryFillBrush => TopCategoryStyles.ResolveFillBrush(TopCategory);
    public Geometry? CategoryIcon => TopCategoryStyles.ResolveIconGeometry(TopCategory);

    public MediaDetailViewModel(
        MediaService mediaService,
        ImageCacheService imageCache,
        ImageService imageService,
        FilesService filesService,
        TagService tagService,
        NineKgTools.Core.Services.Media.CreatorService creatorService,
        FavoriteService favoriteService,
        OpenaiService openaiService,
        NavigationService navigationService,
        IdentificationFlowService identificationFlow,
        IDbContextFactory<MediaDbContext> dbFactory,
        WindowManager windowManager,
        DesktopPreferences preferences)
    {
        _mediaService = mediaService;
        _imageCache = imageCache;
        _imageService = imageService;
        _filesService = filesService;
        _tagService = tagService;
        _creatorService = creatorService;
        _favoriteService = favoriteService;
        _openaiService = openaiService;
        _navigationService = navigationService;
        _identificationFlow = identificationFlow;
        _dbFactory = dbFactory;
        _windowManager = windowManager;
        _preferences = preferences;
        // 初始同步 + 订阅用户在 Settings 里 toggle 的实时变化
        UseGlassBackground = preferences.UseGlassBackground;
        preferences.UseGlassBackgroundChanged += OnUseGlassBackgroundChanged;
    }

    private async void OnUseGlassBackgroundChanged(object? sender, EventArgs e)
    {
        UseGlassBackground = _preferences.UseGlassBackground;
        // toggle on：异步加载模糊图；toggle off：清空（不必丢 LRU 缓存，下次 toggle on 还能命中）
        if (UseGlassBackground)
        {
            await EnsureBlurredPosterAsync();
        }
        else
        {
            BlurredPoster = null;
        }
    }

    /// <summary>当前媒体 Poster 名存在时异步加载模糊版本到 BlurredPoster。
    /// 多次调用 idempotent（ImageCacheService 缓存 + 此处也做"已有"检查）。
    /// 在 LoadAsync 完成 + UseGlassBackground=true 时调一次；用户 toggle on 时再调一次。</summary>
    private async Task EnsureBlurredPosterAsync()
    {
        if (!UseGlassBackground) return;
        var posterName = _media?.Poster?.Name;
        if (string.IsNullOrEmpty(posterName))
        {
            BlurredPoster = null;
            return;
        }
        try
        {
            BlurredPoster = await _imageCache.GetOrLoadBlurredAsync(posterName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载模糊封面失败 Name={Name}", posterName);
            BlurredPoster = null;
        }
    }

    /// <summary>导航进入时调用。NavigationService 在 configureBeforeEnter 阶段已写好 _pendingMediaId
    /// （通过 RequestOpenDetail）；这里 await 触发实际加载。与 Tags/Creators/Circles 的"延迟读取"
    /// 模式一致——避免 sync configureBeforeEnter 内 fire-and-forget 与 OnEnterAsync 的 race。</summary>
    public override async Task OnEnterAsync()
    {
        // 同步 CanGoBack + 订阅变化（NavigationService 历史栈是单 Singleton 共享状态）
        CanGoBack = _navigationService.CanGoBack;
        _navigationService.CanGoBackChanged += OnNavigationCanGoBackChanged;

        if (_pendingMediaId is int id && id > 0)
        {
            _pendingMediaId = null; // 消费一次性参数
            await LoadAsync(id);
        }
    }

    public override Task OnLeaveAsync()
    {
        // 取消订阅避免 Singleton NavigationService / DesktopPreferences 累积 handler 引用导致 VM leak
        _navigationService.CanGoBackChanged -= OnNavigationCanGoBackChanged;
        _preferences.UseGlassBackgroundChanged -= OnUseGlassBackgroundChanged;
        return Task.CompletedTask;
    }

    private void OnNavigationCanGoBackChanged(object? sender, EventArgs e)
    {
        CanGoBack = _navigationService.CanGoBack;
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
            // Cover 加载完后顺带触发模糊版预渲染（仅在偏好开启时）
            // —— 不 await，让 UI 先看到清晰封面再淡入模糊背景
            if (UseGlassBackground)
            {
                _ = EnsureBlurredPosterAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MediaDetailViewModel 加载封面失败：{Name}", posterName);
        }
    }

    private void ApplyToProperties(MediaBase media)
    {
        MediaTitle = media.Title;
        WindowTitle = media.Title;
        CircleName = media.Circle?.Name;
        TopCategory = media.Category?.TopCategory ?? TopCategory.Unknown;
        CategoryDisplayName = TopCategoryStyles.DisplayName(TopCategory);
        CategorySubName = media.Category?.Name ?? "";

        // 按 _media 实际类型分发创作者到职责字段——与 Web 端 EditableCreatorList 完全一致
        ApplyCreatorsByType(media);

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
        EntryFilePath = media.Source?.EntryFilePath;

        ReleaseDateText = media.ReleaseDate?.ToString("yyyy-MM-dd") ?? "—";
        StoreDateText = media.StoreDate?.ToString("yyyy-MM-dd HH:mm") ?? "—";

        Tags = new ObservableCollection<string>(
            media.Tags?.Select(t => t.Name) ?? Enumerable.Empty<string>());

        FavoritePills = new ObservableCollection<FavoritePillViewModel>(
            media.Favorites?.Select(f => new FavoritePillViewModel(f.Name)) ?? Enumerable.Empty<FavoritePillViewModel>());

        RelatedMedias = new ObservableCollection<RelatedMediaItemViewModel>(
            media.RelatedMedias?.Select(r => RelatedMediaItemViewModel.From(r, _imageCache))
            ?? Enumerable.Empty<RelatedMediaItemViewModel>());
    }

    /// <summary>按 _media 的具体子类填充对应职责字段。所有 ObservableCollection 整体替换以触发
    /// HasXxx / HasAnyCreators 派生派发；空集合也要 set（清掉前一次的值）。</summary>
    private void ApplyCreatorsByType(MediaBase media)
    {
        // 先清空所有职责字段，再按类型填——避免上次的 stale 残留
        Directors = new();
        VoiceActors = new();
        ScreenWriters = new();
        Illustrators = new();
        Actors = new();
        Musicians = new();
        Authors = new();
        Makers = new();

        switch (media)
        {
            case VideoMedia v:
                Directors = NamesOf(v.Directors);
                ScreenWriters = NamesOf(v.ScreenWriters);
                Illustrators = NamesOf(v.Illustrators);
                Actors = NamesOf(v.Actors);
                Musicians = NamesOf(v.Musicians);
                Makers = new ObservableCollection<string>(
                    v.Makers?.Where(m => m is not null).Select(m => m.Name).Distinct() ?? Enumerable.Empty<string>());
                break;
            case AudioMedia a:
                VoiceActors = NamesOf(a.VoiceActors);
                ScreenWriters = NamesOf(a.ScreenWriters);
                Illustrators = NamesOf(a.Illustrators);
                Musicians = NamesOf(a.Musicians);
                Authors = NamesOf(a.Authors);
                break;
            case GameMedia g:
                ScreenWriters = NamesOf(g.ScreenWriters);
                Illustrators = NamesOf(g.Illustrators);
                VoiceActors = NamesOf(g.VoiceActors);
                Musicians = NamesOf(g.Musicians);
                Authors = NamesOf(g.Authors);
                break;
            case PictureMedia p:
                Illustrators = NamesOf(p.Illustrators);
                Actors = NamesOf(p.Actors);
                Authors = NamesOf(p.Authors);
                break;
            case TextMedia t:
                Illustrators = NamesOf(t.Illustrators);
                if (t.Author is not null)
                    Authors = new ObservableCollection<string> { t.Author.Name };
                break;
        }

        static ObservableCollection<string> NamesOf(IEnumerable<NineKgTools.Core.Models.Media.Creator>? src)
            => new(src?.Where(c => c is not null).Select(c => c.Name).Distinct() ?? Enumerable.Empty<string>());
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

    /// <summary>设置 / 更改可执行入口文件。所有类型媒体都可用——单文件媒体的 EntryFilePath
    /// 默认 = FullPath，用户也可改成同目录下其他文件（如 .mkv 配 .ass 字幕想点字幕走播放器）；
    /// 文件夹媒体需要明确指定可执行 exe / 主文件。流程：弹 OS 文件选择器
    /// （起始路径 = source 文件夹或 source 所在目录）→ 选定后写回 MediaSource.EntryFilePath
    /// + DbContext 保存。与 Web SourceDetailPage 的 HandleSelectEntryFileAsync 等价，
    /// 只是 UI 入口换成"按钮 + 文件选择器"而非"下拉 + 列表"。</summary>
    [RelayCommand]
    private async Task SetEntryFileAsync()
    {
        if (_media?.Source is null) return;

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            Log.Warning("SetEntryFileAsync: 无法获取 StorageProvider");
            return;
        }

        try
        {
            // 起始目录：文件夹媒体 → 直接用 source 路径；单文件媒体 → 用文件所在目录
            // 这样用户一进来就看到该媒体相关文件，省得自己导航
            Avalonia.Platform.Storage.IStorageFolder? startLocation = null;
            try
            {
                string? startDir = null;
                if (!string.IsNullOrEmpty(FilePath))
                {
                    if (System.IO.Directory.Exists(FilePath))
                        startDir = FilePath;
                    else if (System.IO.File.Exists(FilePath))
                        startDir = System.IO.Path.GetDirectoryName(FilePath);
                }
                if (!string.IsNullOrEmpty(startDir))
                    startLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(startDir);
            }
            catch (Exception ex)
            {
                // 起始路径解析失败不阻断流程，退回到 picker 默认目录
                Log.Debug(ex, "SetEntryFileAsync: 解析起始目录失败，退回默认");
            }

            var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = HasEntryFile ? "更改入口文件" : "选择入口文件（游戏 exe / 入口文件）",
                AllowMultiple = false,
                SuggestedStartLocation = startLocation,
            });
            var file = picked.FirstOrDefault();
            var path = file?.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                return;

            // 持久化到 DB —— Web 那边是直接改 _source.EntryFilePath + DbContext.SaveChangesAsync，
            // 桌面端注入了 IDbContextFactory（Scoped DbContext 用完即释放），同样模式
            var sourceId = _media.Source.Id;
            await using (var db = await _dbFactory.CreateDbContextAsync())
            {
                var tracked = await db.MediaSources.FirstOrDefaultAsync(s => s.Id == sourceId);
                if (tracked is null)
                {
                    Log.Warning("SetEntryFileAsync: MediaSource not found Id={Id}", sourceId);
                    return;
                }
                tracked.EntryFilePath = path;
                await db.SaveChangesAsync();
            }

            // 同步内存模型 + UI
            _media.Source.EntryFilePath = path;
            EntryFilePath = path;
            Log.Information("入口文件已设置：MediaId={MediaId}, Path={Path}", _media.Id, path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SetEntryFileAsync 失败 MediaId={MediaId}", _media?.Id);
        }
    }

    /// <summary>执行入口文件——用 OS shell 默认行为打开：游戏 exe 直接运行，视频 / 音频 / 图片
    /// 走默认播放器或查看器。与 Web SourceDetailPage.HandleOpenEntryFileAsync 等价
    /// （Web 走 IFileExplorerService.RunExecutableAsync / OpenFileWithDefaultAppAsync 区分
    /// 游戏类，桌面端直接 UseShellExecute=true 让 OS 自己判断——更简洁，行为一致）。</summary>
    [RelayCommand]
    private void OpenEntryFile()
    {
        if (string.IsNullOrEmpty(EntryFilePath)) return;
        if (!System.IO.File.Exists(EntryFilePath))
        {
            Log.Warning("入口文件不存在，无法打开：{Path}", EntryFilePath);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = EntryFilePath,
                UseShellExecute = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(EntryFilePath),
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开入口文件失败：{Path}", EntryFilePath);
        }
    }

    [RelayCommand]
    private async Task ReidentifyAsync()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        // 走 IdentificationFlowService：选项对话框 → 进度+诊断对话框 → 预览/入库（与 Web 端
        // SourceDetailPage.HandleReidentifyAsync 行为对齐）。入库成功后 reload 当前媒体——
        // mediaId 在重识别成功后通常不变（FilesService.AddMediaToDatabase 内部已处理 source/media
        // 关联），但保险起见仍按 _media.Id 重载。
        var result = await _identificationFlow.RunInteractiveAsync(FilePath, IdentificationFlowKind.Reidentify);

        if (result == IdentificationFlowResult.Imported && _media != null)
        {
            await LoadAsync(_media.Id);
        }
    }

    // ========== 相关媒体（与 Web MediaPage 即时持久化语义一致） ==========

    /// <summary>编辑模式下点 [+ 添加关联] 触发：弹 MediaSelectorDialog 让用户多选，
    /// diff 出 toAdd/toRemove 分别调 MediaService.AddRelatedMediaAsync / RemoveRelatedMediaAsync
    /// （双向关联），完成后 reload 当前媒体让 UI 拿到新 RelatedMedias。
    /// 与 Web ProcessRelatedMediasSelection 同款，**不走 draft**：双向关联涉及对方媒体的数据，
    /// 纯前端 draft 无法表达完整语义。用户感知 = "改完关闭就生效"。</summary>
    [RelayCommand]
    private async Task AddRelatedMediaAsync()
    {
        if (_media is null) return;
        try
        {
            var current = _media.RelatedMedias ?? new System.Collections.Generic.List<MediaBase>();
            var selectedIds = await MediaSelectorDialog.ShowAsync(
                _mediaService,
                _imageCache,
                excludeMediaId: _media.Id,
                initialSelected: current);

            if (selectedIds is null) return; // 用户取消

            var currentIds = current.Select(m => m.Id).ToHashSet();
            var selectedSet = selectedIds.ToHashSet();

            var toAdd = selectedSet.Except(currentIds).ToList();
            var toRemove = currentIds.Except(selectedSet).ToList();

            foreach (var id in toAdd)
                await _mediaService.AddRelatedMediaAsync(_media.Id, id);
            foreach (var id in toRemove)
                await _mediaService.RemoveRelatedMediaAsync(_media.Id, id);

            if (_media.Id > 0)
                await LoadAsync(_media.Id);

            Log.Information("关联媒体已更新 MediaId={Id} 新增={Add} 移除={Remove}",
                _media.Id, toAdd.Count, toRemove.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "更新关联媒体失败 MediaId={Id}", _media?.Id);
        }
    }

    /// <summary>编辑模式下卡片右上 × 触发：弹 NineKgConfirmDialog Destructive 确认后
    /// 双向解除关联，从 UI collection 立刻移除。</summary>
    [RelayCommand]
    private async Task RemoveRelatedMediaAsync(RelatedMediaItemViewModel? item)
    {
        if (item is null || _media is null) return;

        var ok = await NineKgConfirmDialog.ShowAsync(
            null,
            title: "移除相关媒体",
            message: "两个媒体之间的关联将被解除（双向），媒体本身不会被删除。",
            intent: DialogIntent.Destructive,
            confirmText: "移除",
            targetName: item.Title);
        if (!ok) return;

        try
        {
            await _mediaService.RemoveRelatedMediaAsync(_media.Id, item.Id);
            RelatedMedias.Remove(item);
            _media.RelatedMedias?.RemoveAll(m => m.Id == item.Id);
            OnPropertyChanged(nameof(HasRelatedMedias));
            OnPropertyChanged(nameof(ShowRelatedMediasSection));
            Log.Information("已移除关联媒体 MediaId={Id} RelatedId={Rid}", _media.Id, item.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "移除关联媒体失败 MediaId={Id} RelatedId={Rid}", _media.Id, item.Id);
        }
    }

    /// <summary>点关联媒体卡片整体触发——按 <see cref="Mode"/> 分支：
    /// - EmbeddedPage：NavigationService 主窗内导航替换（历史栈支持 ← 返回）
    /// - IndependentWindow：当前 VM 同窗 LoadAsync 替换（保持独立环境，不污染主窗）
    /// 与 Web /media/{id} 链式导航语义一致</summary>
    [RelayCommand]
    private async Task OpenRelatedMediaAsync(RelatedMediaItemViewModel? item)
    {
        if (item is null || item.Id <= 0) return;
        try
        {
            if (Mode == MediaDetailMode.IndependentWindow)
            {
                // 独立窗内点关联：同窗替换显示新 media（保持独立环境，不污染主窗导航栈）
                await LoadAsync(item.Id);
            }
            else
            {
                // in-page 模式：通过 NavigationService 在主窗内导航——历史栈支持 ← 返回
                await _navigationService.NavigateToAsync<MediaDetailViewModel>(vm =>
                {
                    vm.Mode = MediaDetailMode.EmbeddedPage;
                    vm.RequestOpenDetail(item.Id);
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开关联媒体失败 Id={Id}", item.Id);
        }
    }

    /// <summary>in-page nav bar 右侧 [↗ 在新窗口打开] 按钮——弹独立窗显示同一 media，
    /// 主窗 in-page 保持不动（用户得到两个并行视图）</summary>
    [RelayCommand]
    private void PopOut()
    {
        if (_media is null || _media.Id <= 0) return;
        try
        {
            _windowManager.OpenMediaDetail(_media.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PopOut 失败 Id={Id}", _media.Id);
        }
    }

    /// <summary>in-page nav bar 左侧 [← 返回] 按钮——走 NavigationService 历史栈回上一页。
    /// NavigationService.CanGoBack 控制按钮 IsEnabled（无历史时禁用）</summary>
    [RelayCommand]
    private async Task NavigateBackAsync()
    {
        try
        {
            await _navigationService.NavigateBackAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NavigateBack 失败");
        }
    }

    // ========== 编辑模式：Edit / Cancel / Save / Delete ==========

    [RelayCommand]
    private void EnterEdit()
    {
        if (_media is null || IsEditMode) return;

        // draft：从 _media 拷一份引用列表（编辑过程中改 draft，不动 _media，方便 Cancel）
        _editingTags = _media.Tags?.ToList() ?? new List<Tag>();
        _editingFavorites = _media.Favorites?.ToList() ?? new List<Favorite>();
        _editingCategory = _media.Category;
        // 创作者 per-role draft：按 _media 实际类型拷贝
        InitCreatorDraftsByType(_media);
        // Pictures draft：拷贝 db 实体引用，AddPicture 时往这个 list append 新建 Image，
        // RemoveSelectedPicture 时从这里删；Save 时 diff 出删/增量调 ImageService 处理。
        _editingPictures = _media.Pictures?.ToList() ?? new List<Core.Models.Media.Image>();
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
        _editingFavorites = new();
        _editingCategory = null;
        _editingPictures = new();
        // 重置 per-role creator drafts
        _editingDirectors = new();
        _editingVoiceActors = new();
        _editingScreenWriters = new();
        _editingIllustrators = new();
        _editingActors = new();
        _editingMusicians = new();
        _editingAuthors = new();
        EditingAliases = new ObservableCollection<string>();
        EditingSummaryTranslated = "";
        EditingReleaseDate = null;
        SaveError = null;

        // 字段从 _media 还原（包括 Pictures、Creators 各职责——ApplyToProperties 内部分发）
        if (_media is not null) ApplyToProperties(_media);
        IsEditMode = false;
    }

    /// <summary>编辑模式下点击社团 chip / "+ 添加社团"占位 → 弹 CircleSelectorDialog 从已有 Circle 列表挑一个。
    /// **不在这里做创建 / 改名 / 删除社团等动作** —— 那些都是 Circle 实体本身的内容编辑，应该在 CirclesPage 完成。
    /// 这里只解决"为当前媒体挑社团"这一个动作，所以返回值只能是 db 已存在的 Circle 实例（或 null=取消）。</summary>
    [RelayCommand]
    private async Task PickCircleAsync()
    {
        if (!IsEditMode || _media is null) return;
        try
        {
            var picked = await CircleSelectorDialog.ShowAsync(_media.Circle, _creatorService);
            if (picked is null) return; // 用户取消，保持现状

            _media.Circle = picked;
            CircleName = picked.Name;
            Log.Information("Circle 选择：mediaId={Id}, circle={Name}", _media.Id, picked.Name);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "PickCircleAsync 失败");
            SaveError = "操作失败，请稍后重试。";
        }
    }

    /// <summary>社团 chip 单一交互入口：编辑态弹选择器（PickCircle），浏览态跳转 CirclesPage（OpenCircle）。
    /// 不再为编辑态额外渲染"修改社团 / ✕ 清除"按钮——社团是 1:1 必填语义（每个 media 必须有且仅有一个 Circle），
    /// 用户在媒体页只做"换社团"，社团本身的编辑请去 CirclesPage。</summary>
    [RelayCommand]
    private async Task CircleChipClickAsync()
    {
        if (IsEditMode) await PickCircleAsync();
        else await OpenCircleAsync();
    }

    /// <summary>评分 5 星：CommandParameter "1"-"5"；点击当前已选中星 → 清零（取消评分）。
    /// 与 Web MudRating SelectedValue 逻辑一致：再点一下 = 取消。</summary>
    [RelayCommand]
    private void SetRating(string? scoreStr)
    {
        if (!IsEditMode) return;
        if (!int.TryParse(scoreStr, out var score)) return;
        if (score < 0 || score > 5) return;
        // 点同一星 → 清零（toggle off）
        var current = (int)Math.Round(Rating);
        Rating = current == score ? 0 : score;
        HasRating = Rating > 0;
        RatingText = HasRating ? $"★ {Rating:F1}" : "";
    }

    // ===== 编辑模式下 chip × 删除：标签 / 7 个 Creator 职责 =====
    // chip 内联编辑模式与图片画廊一致：每 chip 右上角 × 弹 NineKgConfirmDialog 确认 → 从 draft 删除。
    // CommandParameter 是 chip 显示的 Name 字符串（同一 section 内 Name 假设唯一——按 FindIndex 删第一项）。

    /// <summary>编辑模式从标签 chip 上点 × 删除——弹 confirm + 删 _editingTags 第一个 Name 匹配项。</summary>
    [RelayCommand]
    private async Task RemoveTagAsync(string? tagName)
    {
        if (!IsEditMode || string.IsNullOrEmpty(tagName)) return;
        var idx = _editingTags.FindIndex(t => t.Name == tagName);
        if (idx < 0) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            ownerVisual: null,
            title: "移除标签",
            message: "只是从此媒体取消关联，标签本身不会被删除。",
            intent: DialogIntent.Destructive,
            targetName: tagName,
            confirmText: "移除");
        if (!confirmed) return;

        _editingTags.RemoveAt(idx);
        // 同步 UI Tags 集合（按相同 Name 找第一个 string 匹配项删）
        var uiIdx = Tags.IndexOf(tagName);
        if (uiIdx >= 0) Tags.RemoveAt(uiIdx);
    }

    [RelayCommand] private Task RemoveDirectorAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.Director, n);
    [RelayCommand] private Task RemoveVoiceActorAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.VoiceActor, n);
    [RelayCommand] private Task RemoveScreenWriterAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.ScreenWriter, n);
    [RelayCommand] private Task RemoveIllustratorAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.Illustrator, n);
    [RelayCommand] private Task RemoveActorAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.Actor, n);
    [RelayCommand] private Task RemoveMusicianAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.Musician, n);
    [RelayCommand] private Task RemoveAuthorAsync(string? n) => RemoveCreatorByRoleAsync(CreatorType.Author, n);

    /// <summary>共享删除逻辑：按 type 找对应 draft + UI 集合，弹 confirm 后删第一个 Name 匹配项。</summary>
    private async Task RemoveCreatorByRoleAsync(CreatorType type, string? creatorName)
    {
        if (!IsEditMode || string.IsNullOrEmpty(creatorName)) return;
        var (current, uiSet, draftSet) = ResolveDraftByType(type);
        var idx = current.FindIndex(c => c.Name == creatorName);
        if (idx < 0) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            ownerVisual: null,
            title: "移除创作者",
            message: "只是从此媒体取消该职责关联，创作者本身不会被删除。",
            intent: DialogIntent.Destructive,
            targetName: creatorName,
            confirmText: "移除");
        if (!confirmed) return;

        var newList = current.ToList();
        newList.RemoveAt(idx);
        draftSet(newList);
        uiSet(new ObservableCollection<string>(newList.Select(c => c.Name).Distinct()));
    }

    // ===== chip 跳转：点击 tag/creator/circle chip 跳到 MediaOverview 并按该实体筛选 =====
    // - Tag 走 Name（MediaQueryParameters.TagNames 接 string）
    // - Creator/Circle 走 Id（精确过滤——同名不同人不会混淆）；从 _media 实体上读取 Id
    // - 编辑模式下 chip 上的 × 优先触发删除，整 chip 的 OpenXxx 命令仅浏览态实际能 fire（编辑态 IsHitTestVisible=false）

    /// <summary>点击标签 chip → 跳 TagsPage 直达该标签详情。
    /// 通过 _media.Tags 找 db Tag 实体的 Id（_media.Tags 由 MediaService.GetMediaAsync 时 Include 过）。
    /// 跨页跳转走 ViewModel.RequestOpenDetail(id) 标记 + OnEnterAsync 消费——避免 configureBeforeEnter
    /// 同步 Action 与 OnEnterAsync 异步加载的 race。</summary>
    [RelayCommand]
    private async Task OpenTagAsync(string? tagName)
    {
        if (string.IsNullOrEmpty(tagName) || _media is null) return;
        var match = _media.Tags?.FirstOrDefault(t => t.Name == tagName);
        if (match is null)
        {
            Log.Debug("OpenTagAsync: 未在 _media.Tags 找到 {Name}（可能是 draft 未保存项），跳过", tagName);
            return;
        }
        try
        {
            await _navigationService.NavigateToAsync<TagsViewModel>(
                vm => vm.RequestOpenDetail(match.Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenTagAsync 失败：{Tag}", tagName);
        }
    }

    /// <summary>点击创作者 chip → 跳 CreatorsPage 直达该创作者详情。</summary>
    [RelayCommand]
    private async Task OpenCreatorAsync(string? creatorName)
    {
        if (string.IsNullOrEmpty(creatorName) || _media is null) return;
        var match = _media.Creators?.FirstOrDefault(c => c.Name == creatorName);
        if (match is null)
        {
            Log.Debug("OpenCreatorAsync: 未在 _media.Creators 找到 {Name}（可能是 draft 未保存项），跳过", creatorName);
            return;
        }
        try
        {
            await _navigationService.NavigateToAsync<CreatorsViewModel>(
                vm => vm.RequestOpenDetail(match.Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenCreatorAsync 失败：{Creator}", creatorName);
        }
    }

    /// <summary>点击社团 chip → 跳 CirclesPage 直达该社团详情。</summary>
    [RelayCommand]
    private async Task OpenCircleAsync()
    {
        if (_media?.Circle is null) return;
        var c = _media.Circle;
        try
        {
            await _navigationService.NavigateToAsync<CirclesViewModel>(
                vm => vm.RequestOpenDetail(c.Id));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenCircleAsync 失败：{Circle}", c.Name);
        }
    }

    /// <summary>SaveAsync 调用——按 media 类型把 per-role draft 写回 _media 各 sub-class 字段，
    /// 然后调 SyncCreators() 把分散字段聚合到 _media.Creators（搜索/向量等其他逻辑用）。</summary>
    private void ApplyCreatorDraftsToMedia(MediaBase media)
    {
        switch (media)
        {
            case VideoMedia v:
                v.Directors = _editingDirectors.ToList();
                v.ScreenWriters = _editingScreenWriters.ToList();
                v.Illustrators = _editingIllustrators.ToList();
                v.Actors = _editingActors.ToList();
                v.Musicians = _editingMusicians.ToList();
                break;
            case AudioMedia a:
                a.VoiceActors = _editingVoiceActors.ToList();
                a.ScreenWriters = _editingScreenWriters.ToList();
                a.Illustrators = _editingIllustrators.ToList();
                a.Musicians = _editingMusicians.ToList();
                a.Authors = _editingAuthors.ToList();
                break;
            case GameMedia g:
                g.ScreenWriters = _editingScreenWriters.ToList();
                g.Illustrators = _editingIllustrators.ToList();
                g.VoiceActors = _editingVoiceActors.ToList();
                g.Musicians = _editingMusicians.ToList();
                g.Authors = _editingAuthors.ToList();
                break;
            case PictureMedia p:
                p.Illustrators = _editingIllustrators.ToList();
                p.Actors = _editingActors.ToList();
                p.Authors = _editingAuthors.ToList();
                break;
            case TextMedia t:
                t.Illustrators = _editingIllustrators.ToList();
                t.Author = _editingAuthors.FirstOrDefault();
                break;
        }
        // 把分散字段聚合到 MediaBase.Creators（保持 Web 端一致；搜索/向量等下游逻辑读这个）
        media.SyncCreators();
    }

    /// <summary>EnterEdit 调用——按 media 类型把对应职责的 Creator 列表 copy 到对应 draft。
    /// Save 时根据同一 type 写回（CancelEdit 不动 draft 之外的 _media 字段，draft 干净抛弃即可）。</summary>
    private void InitCreatorDraftsByType(MediaBase media)
    {
        _editingDirectors = new();
        _editingVoiceActors = new();
        _editingScreenWriters = new();
        _editingIllustrators = new();
        _editingActors = new();
        _editingMusicians = new();
        _editingAuthors = new();

        switch (media)
        {
            case VideoMedia v:
                _editingDirectors = v.Directors?.ToList() ?? new();
                _editingScreenWriters = v.ScreenWriters?.ToList() ?? new();
                _editingIllustrators = v.Illustrators?.ToList() ?? new();
                _editingActors = v.Actors?.ToList() ?? new();
                _editingMusicians = v.Musicians?.ToList() ?? new();
                break;
            case AudioMedia a:
                _editingVoiceActors = a.VoiceActors?.ToList() ?? new();
                _editingScreenWriters = a.ScreenWriters?.ToList() ?? new();
                _editingIllustrators = a.Illustrators?.ToList() ?? new();
                _editingMusicians = a.Musicians?.ToList() ?? new();
                _editingAuthors = a.Authors?.ToList() ?? new();
                break;
            case GameMedia g:
                _editingScreenWriters = g.ScreenWriters?.ToList() ?? new();
                _editingIllustrators = g.Illustrators?.ToList() ?? new();
                _editingVoiceActors = g.VoiceActors?.ToList() ?? new();
                _editingMusicians = g.Musicians?.ToList() ?? new();
                _editingAuthors = g.Authors?.ToList() ?? new();
                break;
            case PictureMedia p:
                _editingIllustrators = p.Illustrators?.ToList() ?? new();
                _editingActors = p.Actors?.ToList() ?? new();
                _editingAuthors = p.Authors?.ToList() ?? new();
                break;
            case TextMedia t:
                _editingIllustrators = t.Illustrators?.ToList() ?? new();
                _editingAuthors = t.Author is not null ? new() { t.Author } : new();
                break;
        }
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

            // 创作者按职责写回（per-role draft → _media 各 sub-class 字段）
            ApplyCreatorDraftsToMedia(_media);

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

            // 图片增删：按 Id 比 _media.Pictures（旧）和 _editingPictures（draft）的差集
            // - 删除：旧里有、新 list 里没的入库实体 → ImageService.RemoveImageAsync 清 db + 文件
            // - 新增：_editingPictures 里 Id==0 且带 Content 的 → ImageService.AddOrFindImagesAsync 入库 + 算 hash + 落 cache
            await ApplyPictureDiffAsync(_media);

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

    /// <summary>编辑某一职责的创作者——string CommandParameter 由 axaml 传（避免 Avalonia 12 enum 直传渲染坑）。
    /// 内部 Enum.TryParse → 路由到对应 draft + UI ObservableCollection；CreatorSelectorDialog 走 initialFilterType
    /// 让 dialog 默认筛选到该 type，与 Web 端 EditableCreatorList 行为对齐。</summary>
    [RelayCommand]
    private async Task EditCreatorsByRoleAsync(string? roleName)
    {
        if (!IsEditMode || _media is null) return;
        if (!Enum.TryParse<CreatorType>(roleName, ignoreCase: true, out var type)) return;
        try
        {
            var (currentDraft, uiCollection, setDraft) = ResolveDraftByType(type);
            var result = await CreatorSelectorDialog.ShowAsync(
                currentDraft,
                allowMultiSelect: true,
                initialFilterType: type,
                _creatorService);
            if (result is null) return;
            setDraft(result);
            // 同步 UI chip 列表（重建以触发 NotifyPropertyChangedFor）
            uiCollection(new ObservableCollection<string>(result.Select(c => c.Name).Distinct()));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditCreatorsByRoleAsync 失败 role={Role}", roleName);
        }
    }

    /// <summary>把 CreatorType 路由到对应 draft list + UI ObservableCollection setter。
    /// Tuple 返回 (当前 draft list, UI collection setter, draft setter)——避免 8 个 if-else 重复。</summary>
    private (List<NineKgTools.Core.Models.Media.Creator> current, Action<ObservableCollection<string>> uiSet, Action<List<NineKgTools.Core.Models.Media.Creator>> draftSet) ResolveDraftByType(CreatorType type) =>
        type switch
        {
            CreatorType.Director => (_editingDirectors, c => Directors = c, l => _editingDirectors = l),
            CreatorType.VoiceActor => (_editingVoiceActors, c => VoiceActors = c, l => _editingVoiceActors = l),
            CreatorType.ScreenWriter => (_editingScreenWriters, c => ScreenWriters = c, l => _editingScreenWriters = l),
            CreatorType.Illustrator => (_editingIllustrators, c => Illustrators = c, l => _editingIllustrators = l),
            CreatorType.Actor => (_editingActors, c => Actors = c, l => _editingActors = l),
            CreatorType.Musician => (_editingMusicians, c => Musicians = c, l => _editingMusicians = l),
            CreatorType.Author => (_editingAuthors, c => Authors = c, l => _editingAuthors = l),
            _ => (new(), _ => { }, _ => { }),
        };

    /// <summary>编辑收藏夹——双模式：编辑模式只改 draft（与其他字段一起 Save），非编辑模式立即 commit
    /// 到 db。收藏夹独立于"全局编辑模式"是产品决定：用户经常只改收藏夹（加入"待看"等），
    /// 走完整编辑流（点编辑 → 改 → 保存 → 退编辑）太重。</summary>
    [RelayCommand]
    private async Task EditFavoritesAsync()
    {
        if (_media is null) return;
        try
        {
            var current = IsEditMode
                ? _editingFavorites
                : (_media.Favorites?.ToList() ?? new List<Favorite>());
            var result = await FavoriteSelectorDialog.ShowAsync(
                current,
                allowMultiSelect: true,
                _favoriteService);
            if (result is null) return;

            if (IsEditMode)
            {
                // 编辑模式：只改 draft，等 SaveAsync 一起 commit
                _editingFavorites = result;
                FavoritePills = new ObservableCollection<FavoritePillViewModel>(
                    result.Select(f => new FavoritePillViewModel(f.Name)));
            }
            else
            {
                // 非编辑模式：立即写回 _media + UpdateMediaAsync 持久化
                _media.Favorites.Clear();
                foreach (var f in result) _media.Favorites.Add(f);
                await _mediaService.UpdateMediaAsync(_media);
                FavoritePills = new ObservableCollection<FavoritePillViewModel>(
                    result.Select(f => new FavoritePillViewModel(f.Name)));
                Log.Information("收藏夹更新（非编辑模式）：mediaId={Id}, count={N}", _media.Id, result.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditFavoritesAsync 失败");
            SaveError = "保存失败，请稍后重试。";
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

    // ===== 图片增删（编辑模式） =====

    /// <summary>添加图片：弹文件选择 → 读 byte[] → 新建 Image 实例（带 Content + GUID 临时 Name）→
    /// 同步加进 _editingPictures（draft）和 Pictures（VM 列表，立即在 UI 显示）。
    /// 实际入库 + cache 文件落地由 SaveAsync 里 ApplyPictureDiffAsync 调 ImageService 处理。</summary>
    [RelayCommand]
    private async Task AddPictureAsync()
    {
        if (!IsEditMode || _media is null) return;

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null)
        {
            Log.Warning("AddPictureAsync: 无法获取 StorageProvider");
            return;
        }

        try
        {
            var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择图片（可多选）",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("图片")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp", "*.bmp", "*.gif" },
                    },
                },
            });
            if (picked.Count == 0) return;

            int addedCount = 0;
            foreach (var file in picked)
            {
                var path = file?.TryGetLocalPath();
                if (string.IsNullOrEmpty(path)) continue;
                try
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    var ext = Path.GetExtension(path);
                    if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                    // Image(byte[], string) ctor 自动算 hash——AddOrFindImageAsync 里靠这个去重
                    var image = new Core.Models.Media.Image(bytes, $"picture_{Guid.NewGuid():N}{ext}");

                    _editingPictures.Add(image);
                    Pictures.Add(new MediaPictureItemViewModel(image, bytes));
                    addedCount++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "AddPictureAsync 单文件读取失败：{Path}", path);
                }
            }
            if (addedCount > 0)
            {
                // 跳到新加的最后一张让用户立刻看到
                SelectedPictureIndex = Pictures.Count - 1;
                // 集合操作不会触发 Pictures setter 的 PropertyChanged——手动通知派生属性重算
                NotifyPicturesDerivedChanged();
                Log.Information("AddPictureAsync: 加入 {N} 张图片到 draft（mediaId={Id}）", addedCount, _media.Id);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AddPictureAsync 失败");
            SaveError = "添加图片失败，请稍后重试。";
        }
    }

    /// <summary>删除指定图片（缩略图 / 主图右上 × 按钮触发）。弹 NineKgConfirmDialog 确认后从
    /// _editingPictures + Pictures 同步移除；真正从 db 移除 + 删 cache 文件由 SaveAsync 里
    /// ApplyPictureDiffAsync 处理（给 Cancel 留撤销机会）。CommandParameter 为目标 VM——null 时
    /// 兜底取 SelectedPicture（主图区 × 按钮可省略 CommandParameter）。</summary>
    [RelayCommand]
    private async Task RemovePictureAsync(MediaPictureItemViewModel? item)
    {
        if (!IsEditMode || _media is null) return;
        var target = item ?? SelectedPicture;
        if (target is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(
            ownerVisual: null,
            title: "删除图片",
            message: "从画廊移除该图片，保存后将从数据库与缓存中清除（其他媒体引用同一张图不受影响）。",
            intent: DialogIntent.Destructive,
            confirmText: "删除");
        if (!confirmed) return;

        var idx = Pictures.IndexOf(target);
        if (idx < 0) return;

        // 从 draft 移除（按引用 — VM 持有的 UnderlyingImage 与 _editingPictures 元素同一实例）
        _editingPictures.Remove(target.UnderlyingImage);
        Pictures.RemoveAt(idx);

        // 调整选中索引：删完 N 张后 currentIdx 可能越界，clamp 到合法范围
        if (Pictures.Count == 0)
        {
            SelectedPictureIndex = 0;
        }
        else if (SelectedPictureIndex >= Pictures.Count)
        {
            SelectedPictureIndex = Pictures.Count - 1;
        }
        else if (idx <= SelectedPictureIndex && SelectedPictureIndex > 0)
        {
            // 删的图在选中之前 → 选中索引前移一位（保证选中的图不变）
            SelectedPictureIndex--;
        }
        else
        {
            // 索引值未变但 SelectedPicture 指向的对象变了——强制 notify 让主图区刷新
            OnPropertyChanged(nameof(SelectedPicture));
            // 同步 IsSelected 标记（OnSelectedPictureIndexChanged partial 不会被 set 同值触发）
            for (int i = 0; i < Pictures.Count; i++)
                Pictures[i].IsSelected = (i == SelectedPictureIndex);
        }
        NotifyPicturesDerivedChanged();
        Log.Information("RemovePicture: 移除 1 张图片到 draft（mediaId={Id}, remaining={N}）",
            _media.Id, Pictures.Count);
    }

    /// <summary>Pictures.Add/Remove 是集合内部变更（不会触发 setter 的 PropertyChanged），
    /// 派生属性（HasPictures / counter / ShowEmpty 等）需要手动 notify。</summary>
    private void NotifyPicturesDerivedChanged()
    {
        OnPropertyChanged(nameof(HasPictures));
        OnPropertyChanged(nameof(HasMultiplePictures));
        OnPropertyChanged(nameof(CanGoPrevPicture));
        OnPropertyChanged(nameof(CanGoNextPicture));
        OnPropertyChanged(nameof(PictureCounterText));
        OnPropertyChanged(nameof(SelectedPicture));
        OnPropertyChanged(nameof(ShowPicturesSection));
        OnPropertyChanged(nameof(ShowEmptyPicturesHint));
    }

    /// <summary>SaveAsync 调用：根据 _editingPictures（draft）和 _media.Pictures（旧）计算增删。
    /// - 删除：旧里有的入库实体（Id>0），不在 draft 里 → 调 ImageService.RemoveImageAsync 清 db + 文件
    /// - 新增：draft 里 Id==0（带 Content）→ 调 ImageService.AddOrFindImagesAsync 入库 + 算 hash + 落 cache，拿回 db 实体
    /// - 保留：draft 里 Id>0 的——直接复用引用
    /// 最后把汇总的 list set 回 _media.Pictures（让 UpdateMediaAsync 持久化关联）。</summary>
    private async Task ApplyPictureDiffAsync(MediaBase media)
    {
        var oldPictures = media.Pictures ?? new List<Core.Models.Media.Image>();
        var draftIds = new HashSet<int>(_editingPictures.Where(p => p.Id > 0).Select(p => p.Id));

        // 1. 删除：旧里有 db record、不在 draft 里
        var toRemove = oldPictures.Where(p => p.Id > 0 && !draftIds.Contains(p.Id)).ToList();
        foreach (var pic in toRemove)
        {
            try
            {
                await _imageService.RemoveImageAsync(pic);
                Log.Information("ApplyPictureDiffAsync: 删除图片 id={Id} name={Name}", pic.Id, pic.Name);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ApplyPictureDiffAsync: 删除图片失败 id={Id}", pic.Id);
            }
        }

        // 2. 新增：draft 里 Id==0 + 带 Content 的——走 ImageService 入库 + 落 cache
        var toAdd = _editingPictures.Where(p => p.Id == 0 && p.Content is { Length: > 0 }).ToList();
        var keepExisting = _editingPictures.Where(p => p.Id > 0).ToList();
        var addedDbEntities = new List<Core.Models.Media.Image>();
        if (toAdd.Count > 0)
        {
            try
            {
                addedDbEntities = await _imageService.AddOrFindImagesAsync(toAdd, media.Title);
                Log.Information("ApplyPictureDiffAsync: 新增 {N} 张图片入库（mediaId={Id}）", addedDbEntities.Count, media.Id);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ApplyPictureDiffAsync: 新图入库失败 mediaId={Id}", media.Id);
            }
        }

        // 3. 汇总：保留旧 + 新增 db 实体——按 draft 里的顺序映射，保证 UI 顺序与编辑顺序一致
        var finalList = new List<Core.Models.Media.Image>();
        foreach (var draftPic in _editingPictures)
        {
            if (draftPic.Id > 0)
            {
                finalList.Add(draftPic);
            }
            else
            {
                // 用 hash 找回对应 db entity（AddOrFindImagesAsync 已按 hash 去重）
                var dbEntity = addedDbEntities.FirstOrDefault(e => e.Hash == draftPic.Hash);
                if (dbEntity is not null) finalList.Add(dbEntity);
            }
        }
        // 注意：用 Clear + Add 维护 EF tracker——直接 set 引用替换在 EF 上 track 行为不稳定
        media.Pictures ??= new List<Core.Models.Media.Image>();
        media.Pictures.Clear();
        foreach (var p in finalList) media.Pictures.Add(p);
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
            // 同步 UI 显示（顶级 brush + 子分类名）；TopCategory 字段上有 NotifyPropertyChangedFor
            // 链 → CategoryBrush / CategoryFillBrush / CategoryIcon 自动 re-evaluate，无需手动 notify
            TopCategory = picked.TopCategory;
            CategoryDisplayName = TopCategoryStyles.DisplayName(picked.TopCategory);
            CategorySubName = picked.Name;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EditCategoryAsync 失败");
        }
    }

    /// <summary>取当前活动 Window 的 TopLevel——给 OS 原生 picker 用。
    /// **多窗口场景必须用 Active 窗口而非 MainWindow**：MediaDetailWindow 是独立窗口，
    /// 若 picker owner 设为 MainWindow，detail 窗在 picker 弹出时失焦 → Windows 当作
    /// "无 modal 父窗"错误最小化（用户感觉是"点设置入口/选图就把详情页缩了"）。
    /// 优先 IsActive 窗口；退化才用 MainWindow（fail-safe）。</summary>
    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            var active = lifetime.Windows.FirstOrDefault(w => w.IsActive);
            if (active is not null)
                return Avalonia.Controls.TopLevel.GetTopLevel(active);
            if (lifetime.MainWindow is not null)
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
