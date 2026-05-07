using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
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

    public override Task OnEnterAsync() => LoadAsync();

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
    private async Task OpenCircleDetailAsync(CircleItemViewModel? item)
    {
        if (item is null) return;
        DetailLoading = true;
        try
        {
            var circle = await _creatorService.GetCircleAsync(item.Id);
            if (circle is null)
            {
                Log.Warning("找不到 Circle: {Id}", item.Id);
                return;
            }
            SelectedCircle = circle;

            DetailAvatar = null;
            var avatarName = circle.Avatar?.Name;
            if (!string.IsNullOrWhiteSpace(avatarName))
            {
                try { DetailAvatar = await _imageCache.GetOrLoadAsync(avatarName); }
                catch (Exception ex) { Log.Warning(ex, "加载社团头像失败: {Id}", item.Id); }
            }

            var medias = await _creatorService.GetCircleMediasAsync(item.Id);
            CircleMedias = new ObservableCollection<MediaCardViewModel>(
                medias.Select(m => new MediaCardViewModel(m, _imageCache)));
            DetailHasMedias = CircleMedias.Count > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载社团详情失败: {Id}", item.Id);
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
