using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.ViewModels.Pages;

/// <summary>
/// 创作者管理。Phase 2.5：列表 / 详情两态切换。详情含别名 + 关联媒体网格 + 合并 / 删除。
/// </summary>
public partial class CreatorsViewModel : PageViewModelBase
{
    private readonly CreatorService _creatorService;
    private readonly ImageCacheService _imageCache;

    public override string Title => "创作者";

    // ========== 列表态 ==========
    [ObservableProperty]
    private ObservableCollection<CreatorItemViewModel> _items = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private int _pageNumber = 1;

    [ObservableProperty]
    private int _pageSize = 30;

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showEmpty = true;

    private CancellationTokenSource? _searchDebounceCts;

    // ========== 详情态 ==========
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowList))]
    [NotifyPropertyChangedFor(nameof(ShowDetail))]
    [NotifyPropertyChangedFor(nameof(ShowDetailReadActions))]
    [NotifyPropertyChangedFor(nameof(ShowDetailEditActions))]
    [NotifyPropertyChangedFor(nameof(DetailName))]
    [NotifyPropertyChangedFor(nameof(DetailAvatarFallback))]
    [NotifyPropertyChangedFor(nameof(DetailTypesText))]
    [NotifyPropertyChangedFor(nameof(DetailAliasText))]
    [NotifyPropertyChangedFor(nameof(HasAlias))]
    [NotifyPropertyChangedFor(nameof(DetailDescription))]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private Creator? _selectedCreator;

    [ObservableProperty]
    private Bitmap? _detailAvatar;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _creatorMedias = new();

    [ObservableProperty]
    private bool _detailLoading;

    [ObservableProperty]
    private bool _detailHasMedias;

    // ========== 详情编辑模式（§4.2 P1） ==========

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailReadActions))]
    [NotifyPropertyChangedFor(nameof(ShowDetailEditActions))]
    private bool _isDetailEditMode;

    [ObservableProperty]
    private bool _isSavingDetail;

    [ObservableProperty]
    private string? _detailSaveError;

    /// <summary>别名 draft（双向绑给 EditableAliasList）</summary>
    [ObservableProperty]
    private ObservableCollection<string> _editingAliases = new();

    /// <summary>描述 draft（双向绑给 TextBox）</summary>
    [ObservableProperty]
    private string _editingDescription = "";

    /// <summary>头像 draft：用户选了新图后存这里，Save 时构造 Image 实例传给 service</summary>
    private byte[]? _editingAvatarBytes;
    private string? _editingAvatarExt;

    public bool ShowDetailReadActions => ShowDetail && !IsDetailEditMode;
    public bool ShowDetailEditActions => ShowDetail && IsDetailEditMode;

    public bool ShowList => SelectedCreator is null;
    public bool ShowDetail => SelectedCreator is not null;
    public string DetailName => SelectedCreator?.Name ?? "—";
    public string DetailAvatarFallback => string.IsNullOrEmpty(SelectedCreator?.Name)
        ? "?"
        : SelectedCreator!.Name[..Math.Min(1, SelectedCreator.Name.Length)].ToUpper();

    public string DetailTypesText => SelectedCreator is null
        ? ""
        : string.Join(" · ", SelectedCreator.Types.Select(MapType));
    public string DetailAliasText => SelectedCreator is null
        ? ""
        : string.Join("、", SelectedCreator.AliasNames);
    public bool HasAlias => SelectedCreator?.AliasNames.Count > 0;
    public string? DetailDescription => SelectedCreator?.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(SelectedCreator?.Description);

    public CreatorsViewModel(CreatorService creatorService, ImageCacheService imageCache)
    {
        _creatorService = creatorService;
        _imageCache = imageCache;
    }

    public override Task OnEnterAsync() => LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var paged = await _creatorService.GetPagedCreatorsAsync(
                PageNumber, PageSize,
                searchTerm: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());

            Items = new ObservableCollection<CreatorItemViewModel>(
                paged.Select(c => new CreatorItemViewModel(c, _imageCache)));
            TotalCount = paged.TotalItemCount;
            TotalPages = paged.PageCount;
            ShowEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Creators 加载失败");
            Items = new ObservableCollection<CreatorItemViewModel>();
            TotalCount = 0;
            TotalPages = 0;
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNextPage))]
    private Task NextPageAsync()
    {
        PageNumber++;
        return LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevPage))]
    private Task PrevPageAsync()
    {
        PageNumber--;
        return LoadAsync();
    }

    private bool CanGoNextPage() => PageNumber < TotalPages;
    private bool CanGoPrevPage() => PageNumber > 1;

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceCts?.Cancel();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested) return;
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    PageNumber = 1;
                    await LoadAsync();
                });
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    // ==================== 详情态命令 ====================

    [RelayCommand]
    private async Task OpenCreatorDetailAsync(CreatorItemViewModel? item)
    {
        if (item is null) return;
        await LoadDetailByIdAsync(item.Id);
    }

    private async Task LoadDetailByIdAsync(int id)
    {
        DetailLoading = true;
        try
        {
            var creator = await _creatorService.GetCreatorAsync(id);
            if (creator is null)
            {
                Log.Warning("找不到 Creator: {Id}", id);
                return;
            }
            SelectedCreator = creator;

            // 头像
            DetailAvatar = null;
            var avatarName = creator.Avatar?.Name;
            if (!string.IsNullOrWhiteSpace(avatarName))
            {
                try { DetailAvatar = await _imageCache.GetOrLoadAsync(avatarName); }
                catch (Exception ex) { Log.Warning(ex, "加载创作者头像失败: {Id}", id); }
            }

            // 关联媒体
            var medias = await _creatorService.GetCreatorMediasAsync(id);
            CreatorMedias = new ObservableCollection<MediaCardViewModel>(
                medias.Select(m => new MediaCardViewModel(m, _imageCache)));
            DetailHasMedias = CreatorMedias.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载创作者详情失败: {Id}", id);
        }
        finally
        {
            DetailLoading = false;
        }
    }

    [RelayCommand]
    private void GoBackToList()
    {
        SelectedCreator = null;
        DetailAvatar = null;
        CreatorMedias = new ObservableCollection<MediaCardViewModel>();
        DetailHasMedias = false;
        IsDetailEditMode = false;
        DetailSaveError = null;
    }

    // ========== 详情编辑模式命令 ==========

    [RelayCommand]
    private void EnterDetailEdit()
    {
        if (SelectedCreator is null || IsDetailEditMode) return;
        EditingAliases = new ObservableCollection<string>(SelectedCreator.AliasNames ?? new List<string>());
        EditingDescription = SelectedCreator.Description ?? "";
        _editingAvatarBytes = null;
        _editingAvatarExt = null;
        DetailSaveError = null;
        IsDetailEditMode = true;
    }

    [RelayCommand]
    private void CancelDetailEdit()
    {
        if (!IsDetailEditMode) return;
        EditingAliases = new ObservableCollection<string>();
        EditingDescription = "";
        _editingAvatarBytes = null;
        _editingAvatarExt = null;
        DetailSaveError = null;
        IsDetailEditMode = false;

        // 还原头像 Bitmap（用户可能选过新图）
        DetailAvatar = null;
        if (SelectedCreator?.Avatar?.Name is { } name)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var bm = await _imageCache.GetOrLoadAsync(name);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => DetailAvatar = bm);
                }
                catch { /* 失败保持 null */ }
            });
        }
    }

    [RelayCommand]
    private async Task ChangeAvatarAsync()
    {
        if (!IsDetailEditMode || SelectedCreator is null) return;

        var topLevel = GetTopLevel();
        if (topLevel?.StorageProvider is null) return;

        try
        {
            var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择头像图片",
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
            _editingAvatarBytes = bytes;
            _editingAvatarExt = Path.GetExtension(path);

            // UI 即时反馈
            using var ms = new MemoryStream(bytes);
            DetailAvatar = new Bitmap(ms);
            Log.Information("ChangeAvatarAsync: 已加载新头像 {Path} ({Size} bytes)", path, bytes.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChangeAvatarAsync 失败");
            DetailSaveError = "头像加载失败，请稍后重试。";
        }
    }

    [RelayCommand]
    private async Task SaveDetailAsync()
    {
        if (SelectedCreator is null || !IsDetailEditMode || IsSavingDetail) return;
        IsSavingDetail = true;
        DetailSaveError = null;

        try
        {
            // 用 draft 字段构造 Creator 实例传给 FindAndUpdateCreatorAsync
            var updated = new Creator
            {
                Id = SelectedCreator.Id,
                Name = SelectedCreator.Name,
                AliasNames = EditingAliases.ToList(),
                Description = string.IsNullOrWhiteSpace(EditingDescription) ? null : EditingDescription.Trim(),
                Types = SelectedCreator.Types?.ToList() ?? new List<CreatorType>(),
            };

            // 头像：用户选过新图 → 新 Image（service 内 AddOrFindImageAsync 走 hash 去重 + 写 .cache）；
            // 没选则保留原值
            if (_editingAvatarBytes is not null)
            {
                updated.Avatar = new Image
                {
                    Name = $"creator_{SelectedCreator.Id}_{Guid.NewGuid():N}{_editingAvatarExt ?? ".jpg"}",
                    Content = _editingAvatarBytes,
                };
            }
            else
            {
                updated.Avatar = SelectedCreator.Avatar;
            }

            await _creatorService.FindAndUpdateCreatorAsync(updated);
            Log.Information("CreatorDetail 保存成功：Id={Id}, Name={Name}", updated.Id, updated.Name);

            // 重新加载详情拿真实 db 状态
            await LoadDetailByIdAsync(SelectedCreator.Id);
            _editingAvatarBytes = null;
            _editingAvatarExt = null;
            IsDetailEditMode = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CreatorDetail 保存失败：Id={Id}", SelectedCreator?.Id);
            DetailSaveError = "保存失败，请稍后重试。";
        }
        finally
        {
            IsSavingDetail = false;
        }
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedCreator is null) return;
        var creator = SelectedCreator;
        var mediaCount = CreatorMedias.Count;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除创作者",
            message: $"将删除创作者「{creator.Name}」并解除与 {mediaCount} 件作品的关联。**作品本身不会被删除**。",
            intent: DialogIntent.Destructive,
            targetName: creator.Name,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _creatorService.DeleteCreatorAsync(creator.Id);
            GoBackToList();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除创作者失败: {Id}", creator.Id);
        }
    }

    [RelayCommand]
    private async Task MergeSelectedAsync()
    {
        if (SelectedCreator is null) return;
        var source = SelectedCreator;

        // 拉全部创作者，附带 Medias 计数
        var allCreators = await _creatorService.GetAllCreatorsAsync();
        // GetAllCreatorsAsync 不 Include Medias，需要补 medias 数（避免 N+1，这里轻量从 db 读 Medias.Count 用一个 Aggregate 查询代价大；先简化为按需在 dialog 后再查）
        // 简化策略：直接传不含 Medias 的列表，dialog 里只展示名字 + AliasNames；预览的"目标创作者已有 N 件"用 0 占位（或后续优化）
        // 但为了影响预览准确，这里给 source 自己补 mediaCount
        source.Medias = (await _creatorService.GetCreatorMediasAsync(source.Id))
            .Cast<MediaBase>().ToList();

        var target = await CreatorMergeDialog.ShowAsync(source, allCreators);
        if (target is null) return;

        try
        {
            // 获取双方实际媒体 ID
            var sourceMedias = await _creatorService.GetCreatorMediasAsync(source.Id);
            var targetMedias = await _creatorService.GetCreatorMediasAsync(target.Id);

            var unionIds = sourceMedias.Select(m => m.Id)
                .Concat(targetMedias.Select(m => m.Id))
                .Distinct()
                .ToList();

            await _creatorService.UpdateCreatorMediasAsync(target.Id, unionIds);
            await _creatorService.DeleteCreatorAsync(source.Id);

            Log.Information("合并创作者成功: {SourceId}({SourceName}) -> {TargetId}({TargetName}), {Count} 件作品迁移",
                source.Id, source.Name, target.Id, target.Name, sourceMedias.Count);

            GoBackToList();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "合并创作者失败: {SourceId} -> {TargetId}", source.Id, target.Id);
        }
    }

    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is not null)
        {
            return Avalonia.Controls.TopLevel.GetTopLevel(lifetime.MainWindow);
        }
        return null;
    }

    private static string MapType(CreatorType t) => t switch
    {
        CreatorType.Author => "作者",
        CreatorType.Illustrator => "画师",
        CreatorType.Musician => "音乐",
        CreatorType.ScreenWriter => "编剧",
        CreatorType.VoiceActor => "声优",
        CreatorType.Director => "导演",
        CreatorType.Actor => "演员",
        _ => t.ToString()
    };
}
