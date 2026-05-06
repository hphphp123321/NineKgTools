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
