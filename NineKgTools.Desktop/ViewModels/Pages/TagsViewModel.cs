using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 标签管理。Phase 2.4 MVP：单页内 IsVisible 切换"顶级列表 / 顶级详情"两态，
/// 单标签详情（媒体网格）后续接入 MediaShownView 时再开第三层。
/// </summary>
public partial class TagsViewModel : PageViewModelBase
{
    private readonly TagService _tagService;
    private readonly MediaService _mediaService;
    private readonly ImageCacheService _imageCache;
    private readonly NavigationService _navigation;

    /// <summary>标签详情页媒体数硬上限——超过提示用户改用媒体库的多维筛选。</summary>
    private const int MaxMediasOnTagDetail = 200;

    public override string Title => "标签";

    [ObservableProperty]
    private ObservableCollection<TopTagItemViewModel> _topTags = new();

    [ObservableProperty]
    private ObservableCollection<TagItemViewModel> _tags = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTopList))]
    [NotifyPropertyChangedFor(nameof(ShowTopDetail))]
    [NotifyPropertyChangedFor(nameof(SelectedTopName))]
    [NotifyPropertyChangedFor(nameof(SelectedTopCountText))]
    private TopTagItemViewModel? _selectedTopTag;

    /// <summary>三态枢纽：选中具体子标签时进入"标签详情"，看其关联媒体网格。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTopList))]
    [NotifyPropertyChangedFor(nameof(ShowTopDetail))]
    [NotifyPropertyChangedFor(nameof(ShowTagDetail))]
    [NotifyPropertyChangedFor(nameof(SelectedTagName))]
    [NotifyPropertyChangedFor(nameof(SelectedTagDescription))]
    [NotifyPropertyChangedFor(nameof(HasTagDescription))]
    [NotifyPropertyChangedFor(nameof(SelectedTagMediaCountText))]
    private TagItemViewModel? _selectedTagDetail;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _tagMedias = new();

    [ObservableProperty]
    private bool _detailLoading;

    [ObservableProperty]
    private bool _detailHasMedias;

    [ObservableProperty]
    private bool _detailMediasTruncated;

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmptyTopList;

    [ObservableProperty]
    private bool _showEmptyTagList;

    public bool ShowTopList => SelectedTopTag is null && SelectedTagDetail is null;
    public bool ShowTopDetail => SelectedTopTag is not null && SelectedTagDetail is null;
    public bool ShowTagDetail => SelectedTagDetail is not null;
    public string SelectedTopName => SelectedTopTag?.Name ?? "—";
    public string SelectedTopCountText => SelectedTopTag is null
        ? ""
        : $"({Tags.Count} 个标签)";
    public string SelectedTagName => SelectedTagDetail?.Name ?? "—";
    public string? SelectedTagDescription => SelectedTagDetail?.Description;
    public bool HasTagDescription => !string.IsNullOrWhiteSpace(SelectedTagDescription);
    public string SelectedTagMediaCountText => SelectedTagDetail is null
        ? ""
        : $"({TagMedias.Count} 条媒体" + (DetailMediasTruncated ? $" · 仅展示前 {MaxMediasOnTagDetail}" : "") + ")";

    private List<TagItemViewModel> _allTagsForSelectedTop = new();

    public TagsViewModel(TagService tagService, MediaService mediaService, ImageCacheService imageCache,
        NavigationService navigation)
    {
        _tagService = tagService;
        _mediaService = mediaService;
        _imageCache = imageCache;
        _navigation = navigation;
    }

    /// <summary>跳到"标签映射"子页面（§3.3）。从 NavigationView 移除独立入口后由本按钮承接。</summary>
    [RelayCommand]
    private Task OpenTagMappings() => _navigation.NavigateToAsync<TagsMappingsViewModel>();

    public override Task OnEnterAsync() => LoadTopTagsAsync();

    [RelayCommand]
    private async Task LoadTopTagsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var topTags = await _tagService.GetCopiedTopTagsAsync();
            var allTags = await _tagService.GetAllTagsAsync();
            var countByTopId = allTags
                .Where(t => t.TopTag != null)
                .GroupBy(t => t.TopTag.Id)
                .ToDictionary(g => g.Key, g => g.Count());

            TopTags = new ObservableCollection<TopTagItemViewModel>(
                topTags.Select(t => new TopTagItemViewModel(
                    t,
                    countByTopId.TryGetValue(t.Id, out var c) ? c : 0)));
            ShowEmptyTopList = TopTags.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载顶级标签失败");
            TopTags = new ObservableCollection<TopTagItemViewModel>();
            ShowEmptyTopList = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SelectTopTagAsync(TopTagItemViewModel? item)
    {
        if (item is null) return;

        SelectedTopTag = item;
        SearchText = "";
        IsLoading = true;
        try
        {
            var children = await _tagService.GetTagsByTopTagIdAsync(item.Id);
            _allTagsForSelectedTop = children
                .OrderByDescending(t => t.Medias?.Count ?? 0)
                .ThenBy(t => t.Name)
                .Select(t => new TagItemViewModel(t))
                .ToList();
            ApplyTagFilter();
            OnPropertyChanged(nameof(SelectedTopCountText));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载子标签失败：{Id}", item.Id);
            _allTagsForSelectedTop = new List<TagItemViewModel>();
            Tags = new ObservableCollection<TagItemViewModel>();
            ShowEmptyTagList = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoBackToTopList()
    {
        SelectedTopTag = null;
        Tags = new ObservableCollection<TagItemViewModel>();
        _allTagsForSelectedTop = new List<TagItemViewModel>();
        SearchText = "";
    }

    // ==================== 标签详情态（第三层） ====================

    [RelayCommand]
    private async Task OpenTagDetailAsync(TagItemViewModel? item)
    {
        if (item is null) return;

        SelectedTagDetail = item;
        DetailLoading = true;
        TagMedias = new ObservableCollection<MediaCardViewModel>();
        DetailHasMedias = false;
        DetailMediasTruncated = false;

        try
        {
            // 走 MediaService.GetPagedMediaList + TagNames 过滤——比 Tag.Medias.ToList()
            // 完整 Include 字段（Poster/Circle/Category），卡片渲染需要这些
            var parameters = new MediaQueryParameters
            {
                TagNames = new List<string> { item.Name },
                FilterByTopCategoryOnly = true,
                SortOption = MediaSortOption.IdDesc,
                PageNumber = 1,
                PageSize = MaxMediasOnTagDetail,
            };
            var paged = _mediaService.GetPagedMediaList(parameters);

            TagMedias = new ObservableCollection<MediaCardViewModel>(
                paged.Select(m => new MediaCardViewModel(m, _imageCache)));
            DetailHasMedias = TagMedias.Count > 0;
            DetailMediasTruncated = paged.TotalItemCount > MaxMediasOnTagDetail;
            OnPropertyChanged(nameof(SelectedTagMediaCountText));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载标签关联媒体失败：{Id}", item.Id);
        }
        finally
        {
            DetailLoading = false;
        }
    }

    [RelayCommand]
    private void GoBackToTagsList()
    {
        SelectedTagDetail = null;
        TagMedias = new ObservableCollection<MediaCardViewModel>();
        DetailHasMedias = false;
        DetailMediasTruncated = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyTagFilter();
    }

    private void ApplyTagFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allTagsForSelectedTop
            : _allTagsForSelectedTop
                .Where(t => t.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                            || (t.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        Tags = new ObservableCollection<TagItemViewModel>(filtered);
        ShowEmptyTagList = Tags.Count == 0;
        OnPropertyChanged(nameof(SelectedTopCountText));
    }

    // ==================== 顶级标签：新增 / 重命名 / 删除 ====================

    [RelayCommand]
    private async Task AddTopTagAsync()
    {
        var name = await TopTagEditorDialog.ShowAsync(title: "新建顶级分组");
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            await _tagService.AddTopTagAsync(new TopTag { Name = name });
            await LoadTopTagsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "新建顶级标签失败：{Name}", name);
        }
    }

    [RelayCommand]
    private async Task RenameTopTagAsync(TopTagItemViewModel? item)
    {
        if (item is null) return;

        var newName = await TopTagEditorDialog.ShowAsync(
            title: "重命名顶级分组",
            initialName: item.Name,
            childCount: item.ChildCount);
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name) return;

        try
        {
            await _tagService.UpdateTopTagAsync(new TopTag { Id = item.Id, Name = newName });
            await LoadTopTagsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重命名顶级标签失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteTopTagAsync(TopTagItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除顶级分组",
            message: $"将删除分组「{item.Name}」及其下 {item.ChildCount} 个子标签。**所有子标签与媒体的关联会被解除**，但媒体本身不会被删除。",
            intent: DialogIntent.Destructive,
            targetName: item.Name,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _tagService.RemoveTopTagAsync(new TopTag { Id = item.Id, Name = item.Name });
            await LoadTopTagsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除顶级标签失败：{Id}", item.Id);
        }
    }

    // ==================== 子标签：新增 / 编辑 / 删除 ====================

    [RelayCommand]
    private async Task AddTagAsync()
    {
        var topTags = await _tagService.GetCopiedTopTagsAsync();
        if (topTags.Count == 0)
        {
            Log.Warning("无可用顶级分组，无法新建子标签");
            return;
        }

        // 默认选中当前进入的那个顶级分组（如果用户在详情态）
        var preselect = SelectedTopTag is not null
            ? topTags.FirstOrDefault(t => t.Id == SelectedTopTag.Id)
            : null;

        var result = await TagEditorDialog.ShowAsync(
            title: "新建标签",
            availableTopTags: topTags,
            initialTopTag: preselect);
        if (result is null) return;

        try
        {
            await _tagService.AddTagAsync(new Tag
            {
                Name = result.Name,
                Description = result.Description,
                TopTag = result.TopTag,
            });

            // 视情况刷新
            if (SelectedTopTag is not null)
                await SelectTopTagAsync(SelectedTopTag);
            else
                await LoadTopTagsAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "新建子标签失败：{Name}", result.Name);
        }
    }

    [RelayCommand]
    private async Task EditTagAsync(TagItemViewModel? item)
    {
        if (item is null) return;

        var topTags = await _tagService.GetCopiedTopTagsAsync();
        var result = await TagEditorDialog.ShowAsync(
            title: "编辑标签",
            availableTopTags: topTags,
            initialTopTag: item.Source.TopTag,
            initialName: item.Name,
            initialDescription: item.Description,
            mediaCount: item.MediaCount);
        if (result is null) return;

        try
        {
            await _tagService.UpdateTagAsync(new Tag
            {
                Id = item.Id,
                Name = result.Name,
                Description = result.Description,
                TopTag = result.TopTag,
            });

            // 重命名 / 改顶级后都重新拉当前列表（顶级如果改了，当前列表也可能少一项）
            if (SelectedTopTag is not null)
                await SelectTopTagAsync(SelectedTopTag);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "编辑子标签失败：{Id}", item.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteTagAsync(TagItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除标签",
            message: $"将解除 {item.MediaCount} 条媒体与「{item.Name}」的关联。**媒体本身不会被删除**。",
            intent: DialogIntent.Destructive,
            targetName: item.Name,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _tagService.RemoveTagAsync(new Tag { Id = item.Id, Name = item.Name });
            if (SelectedTopTag is not null)
                await SelectTopTagAsync(SelectedTopTag);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除子标签失败：{Id}", item.Id);
        }
    }
}
