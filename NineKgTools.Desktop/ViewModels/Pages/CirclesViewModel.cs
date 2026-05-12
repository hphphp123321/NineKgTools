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
/// 社团管理。与 CreatorsViewModel 同款双态结构（列表 ↔ 详情嵌入），但去掉 Types 筛选与合并。
/// 创作者侧的"合并到..." UX 在社团场景使用频率低（多数手动 dedup），先不做——按 §13 第五波最小可用。
/// </summary>
public partial class CirclesViewModel : PageViewModelBase
{
    private readonly CreatorService _creatorService; // CreatorService 兼任 CircleService 角色
    private readonly ImageCacheService _imageCache;

    public override string Title => "社团";

    // ========== 列表态 ==========
    [ObservableProperty]
    private ObservableCollection<CircleItemViewModel> _items = new();

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
    [NotifyPropertyChangedFor(nameof(DetailAliasText))]
    [NotifyPropertyChangedFor(nameof(HasAlias))]
    [NotifyPropertyChangedFor(nameof(DetailDescription))]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    private Circle? _selectedCircle;

    [ObservableProperty]
    private Bitmap? _detailAvatar;

    [ObservableProperty]
    private ObservableCollection<MediaCardViewModel> _circleMedias = new();

    [ObservableProperty]
    private bool _detailLoading;

    [ObservableProperty]
    private bool _detailHasMedias;

    // ========== 详情编辑模式（§4.4 P1） ==========

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDetailReadActions))]
    [NotifyPropertyChangedFor(nameof(ShowDetailEditActions))]
    private bool _isDetailEditMode;

    [ObservableProperty]
    private bool _isSavingDetail;

    [ObservableProperty]
    private string? _detailSaveError;

    [ObservableProperty]
    private ObservableCollection<string> _editingAliases = new();

    [ObservableProperty]
    private string _editingDescription = "";

    /// <summary>头像 draft（用户选了新图后存这里，Save 时构造 Image 传给 service）</summary>
    private byte[]? _editingAvatarBytes;
    private string? _editingAvatarExt;

    public bool ShowDetailReadActions => ShowDetail && !IsDetailEditMode;
    public bool ShowDetailEditActions => ShowDetail && IsDetailEditMode;

    public bool ShowList => SelectedCircle is null;
    public bool ShowDetail => SelectedCircle is not null;
    public string DetailName => SelectedCircle?.Name ?? "—";
    public string DetailAvatarFallback => string.IsNullOrEmpty(SelectedCircle?.Name)
        ? "?"
        : SelectedCircle!.Name[..Math.Min(1, SelectedCircle.Name.Length)].ToUpper();
    public string DetailAliasText => SelectedCircle is null
        ? ""
        : string.Join("、", SelectedCircle.AliasNames);
    public bool HasAlias => SelectedCircle?.AliasNames.Count > 0;
    public string? DetailDescription => SelectedCircle?.Description;
    public bool HasDescription => !string.IsNullOrWhiteSpace(SelectedCircle?.Description);

    public CirclesViewModel(CreatorService creatorService, ImageCacheService imageCache)
    {
        _creatorService = creatorService;
        _imageCache = imageCache;
    }

    /// <summary>跨页跳转 pending（同 CreatorsViewModel）——MediaDetailWindow 社团 chip 跳转用。</summary>
    private int? _pendingDetailId;

    public void RequestOpenDetail(int id) => _pendingDetailId = id;

    public override async Task OnEnterAsync()
    {
        await LoadAsync();
        if (_pendingDetailId is { } pid)
        {
            _pendingDetailId = null;
            await OpenDetailByIdAsync(pid);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var paged = await _creatorService.GetPagedCirclesAsync(
                PageNumber, PageSize,
                searchTerm: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());

            Items = new ObservableCollection<CircleItemViewModel>(
                paged.Select(c => new CircleItemViewModel(c, _imageCache)));
            TotalCount = paged.TotalItemCount;
            TotalPages = paged.PageCount;
            ShowEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Circles 加载失败");
            Items = new ObservableCollection<CircleItemViewModel>();
            TotalCount = 0;
            TotalPages = 0;
            ShowEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>新建社团——弹 CircleEditorDialog → CreateCircleAsync → 刷新列表 + 跳详情。
    /// Core service 同名允许，不做重名校验（与 Web 一致）。</summary>
    [RelayCommand]
    private async Task AddCircleAsync()
    {
        var name = await CircleEditorDialog.ShowAsync();
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            var newCircle = await _creatorService.CreateCircleAsync(name);
            Log.Information("新建社团: {Name} (Id={Id})", newCircle.Name, newCircle.Id);
            await LoadAsync();
            await LoadDetailByIdAsync(newCircle.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "新建社团失败 Name={Name}", name);
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

    private static Avalonia.Controls.TopLevel? GetTopLevel()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime
            && lifetime.MainWindow is not null)
        {
            return Avalonia.Controls.TopLevel.GetTopLevel(lifetime.MainWindow);
        }
        return null;
    }

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
    private Task OpenCircleDetailAsync(CircleItemViewModel? item) =>
        item is null ? Task.CompletedTask : LoadDetailByIdAsync(item.Id);

    /// <summary>外部入口（如 MediaDetailWindow chip 跳转）：直接进入指定 Circle 详情。</summary>
    public Task OpenDetailByIdAsync(int id) => LoadDetailByIdAsync(id);

    private async Task LoadDetailByIdAsync(int id)
    {
        DetailLoading = true;
        try
        {
            var circle = await _creatorService.GetCircleAsync(id);
            if (circle is null)
            {
                Log.Warning("找不到 Circle: {Id}", id);
                return;
            }
            SelectedCircle = circle;

            DetailAvatar = null;
            var avatarName = circle.Avatar?.Name;
            if (!string.IsNullOrWhiteSpace(avatarName))
            {
                try { DetailAvatar = await _imageCache.GetOrLoadAsync(avatarName); }
                catch (Exception ex) { Log.Warning(ex, "加载社团头像失败: {Id}", id); }
            }

            var medias = await _creatorService.GetCircleMediasAsync(id);
            CircleMedias = new ObservableCollection<MediaCardViewModel>(
                medias.Select(m => new MediaCardViewModel(m, _imageCache)));
            DetailHasMedias = CircleMedias.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载社团详情失败: {Id}", id);
        }
        finally
        {
            DetailLoading = false;
        }
    }

    [RelayCommand]
    private void GoBackToList()
    {
        SelectedCircle = null;
        DetailAvatar = null;
        CircleMedias = new ObservableCollection<MediaCardViewModel>();
        DetailHasMedias = false;
        IsDetailEditMode = false;
        DetailSaveError = null;
    }

    // ========== 详情编辑模式 ==========

    [RelayCommand]
    private void EnterDetailEdit()
    {
        if (SelectedCircle is null || IsDetailEditMode) return;
        EditingAliases = new ObservableCollection<string>(SelectedCircle.AliasNames ?? new List<string>());
        EditingDescription = SelectedCircle.Description ?? "";
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
        if (SelectedCircle?.Avatar?.Name is { } name)
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
        if (!IsDetailEditMode || SelectedCircle is null) return;

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

            using var ms = new MemoryStream(bytes);
            DetailAvatar = new Bitmap(ms);
            Log.Information("Circle ChangeAvatarAsync: 已加载新头像 {Path} ({Size} bytes)", path, bytes.Length);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Circle ChangeAvatarAsync 失败");
            DetailSaveError = "头像加载失败，请稍后重试。";
        }
    }

    [RelayCommand]
    private async Task SaveDetailAsync()
    {
        if (SelectedCircle is null || !IsDetailEditMode || IsSavingDetail) return;
        IsSavingDetail = true;
        DetailSaveError = null;

        try
        {
            var updated = new Circle
            {
                Id = SelectedCircle.Id,
                Name = SelectedCircle.Name,
                AliasNames = EditingAliases.ToList(),
                Description = string.IsNullOrWhiteSpace(EditingDescription) ? null : EditingDescription.Trim(),
            };

            // 头像：用户选过新图 → 新 Image；否则保留原值
            if (_editingAvatarBytes is not null)
            {
                updated.Avatar = new Image
                {
                    Name = $"circle_{SelectedCircle.Id}_{Guid.NewGuid():N}{_editingAvatarExt ?? ".jpg"}",
                    Content = _editingAvatarBytes,
                };
            }
            else
            {
                updated.Avatar = SelectedCircle.Avatar;
            }

            await _creatorService.FindAndUpdateCircleAsync(updated);
            Log.Information("CircleDetail 保存成功：Id={Id}, Name={Name}", updated.Id, updated.Name);

            // 重新加载详情拿真实 db 状态——复用 OpenCircleDetailAsync 但需要 CircleItemViewModel
            // 这里直接 GetCircleAsync + 重设字段（避免重新构造列表 VM）
            var refreshed = await _creatorService.GetCircleAsync(SelectedCircle.Id);
            if (refreshed is not null)
            {
                SelectedCircle = refreshed;
                // 如果换了头像，重新拉新文件名的 Bitmap（_imageCache.GetOrLoadAsync 会从 db Content 兜底）
                if (refreshed.Avatar?.Name is { } name)
                {
                    try { DetailAvatar = await _imageCache.GetOrLoadAsync(name); }
                    catch { /* 保持 null */ }
                }
            }
            _editingAvatarBytes = null;
            _editingAvatarExt = null;
            IsDetailEditMode = false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CircleDetail 保存失败：Id={Id}", SelectedCircle?.Id);
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
        if (SelectedCircle is null) return;
        var circle = SelectedCircle;
        var mediaCount = CircleMedias.Count;

        var confirmed = await NineKgConfirmDialog.ShowAsync(null,
            title: "删除社团",
            message: $"将删除社团「{circle.Name}」并解除与 {mediaCount} 件作品的关联。**作品本身不会被删除**。",
            intent: DialogIntent.Destructive,
            targetName: circle.Name,
            confirmText: "确认删除");
        if (!confirmed) return;

        try
        {
            await _creatorService.DeleteCircleAsync(circle.Id);
            GoBackToList();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "删除社团失败: {Id}", circle.Id);
        }
    }
}
