using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using Serilog;
using X.PagedList;

namespace NineKgTools.Pages.Creators;

public partial class CreatorsPage : ComponentBase
{
    [Inject] private CreatorService CreatorService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private IPagedList<Creator>? _pagedCreators;
    private int _totalCreatorCount;
    private bool _isLoading;
    private string _searchTerm = string.Empty;
    private CreatorType? _selectedType;

    // 分页
    private int _currentPage = 1;
    private int _totalPages;
    private int _pageSize = 24;
    private static readonly int[] AllowedPageSizes = { 12, 24, 48, 96 };

    // 新增 Creator 相关
    private bool _showAddDialog;
    private string _newCreatorName = string.Empty;
    private List<CreatorType> _newCreatorTypes = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadCreators();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task LoadCreators()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            _totalCreatorCount = await CreatorService.GetCreatorCountAsync();
            _pagedCreators = await CreatorService.GetPagedCreatorsAsync(
                _currentPage, _pageSize, _searchTerm, _selectedType);
            _totalPages = _pagedCreators.PageCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载创作者列表失败");
            Snackbar.Add("加载创作者列表失败，请稍后重试。", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    // 搜索/筛选变化时重置到第 1 页
    private async Task HandleSearchTermChanged()
    {
        _currentPage = 1;
        await LoadCreators();
    }

    private async Task HandleTypeFilterChanged(CreatorType? type)
    {
        _selectedType = type;
        _currentPage = 1;
        await LoadCreators();
    }

    private async Task OnPageChanged(int page)
    {
        if (page == _currentPage) return;
        _currentPage = page;
        await LoadCreators();
    }

    private async Task OnPageSizeChanged(int newSize)
    {
        if (newSize == _pageSize) return;
        _pageSize = newSize;
        _currentPage = 1;
        await LoadCreators();
    }

    /// <summary>
    /// 打开新增Creator对话框
    /// </summary>
    private void OpenAddDialog()
    {
        _newCreatorName = string.Empty;
        _newCreatorTypes = new List<CreatorType>();
        _showAddDialog = true;
    }

    /// <summary>
    /// 关闭新增Creator对话框
    /// </summary>
    private void CloseAddDialog()
    {
        _showAddDialog = false;
    }

    /// <summary>
    /// 切换Creator类型选择
    /// </summary>
    private void ToggleCreatorType(CreatorType type)
    {
        if (_newCreatorTypes.Contains(type))
        {
            _newCreatorTypes.Remove(type);
        }
        else
        {
            _newCreatorTypes.Add(type);
        }
    }

    /// <summary>
    /// 创建新Creator
    /// </summary>
    private async Task CreateCreator()
    {
        if (string.IsNullOrWhiteSpace(_newCreatorName))
        {
            Snackbar.Add("请输入创作者名称", Severity.Warning);
            return;
        }

        try
        {
            var newCreator = await CreatorService.CreateCreatorAsync(_newCreatorName, _newCreatorTypes);
            Snackbar.Add($"已创建创作者: {newCreator.Name}", Severity.Success);

            CloseAddDialog();

            // 跳转到新创建的Creator详情页
            NavigationManager.NavigateTo($"/creator/{newCreator.Id}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建创作者失败");
            Snackbar.Add($"创建创作者失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 删除Creator
    /// </summary>
    private async Task DeleteCreator(Creator creator)
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "删除创作者",
            "删除后该创作者与其所有作品的关联会被解除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: creator.Name,
            targetIcon: Icons.Material.Filled.Person);

        if (confirmed)
        {
            try
            {
                await CreatorService.DeleteCreatorAsync(creator.Id);
                Snackbar.Add($"已删除创作者: {creator.Name}", Severity.Success);
                await LoadCreators();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除创作者失败 CreatorId={Id}", creator.Id);
                Snackbar.Add("删除创作者失败，请稍后重试。", Severity.Error);
            }
        }
    }

    /// <summary>
    /// 导航到Creator详情页
    /// </summary>
    private void NavigateToCreator(Creator creator)
    {
        NavigationManager.NavigateTo($"/creator/{creator.Id}");
    }

    /// <summary>
    /// 获取CreatorType的显示名称
    /// </summary>
    private string GetCreatorTypeName(CreatorType type) => type switch
    {
        CreatorType.Author => "作者",
        CreatorType.Illustrator => "画师",
        CreatorType.Musician => "音乐",
        CreatorType.ScreenWriter => "编剧",
        CreatorType.VoiceActor => "声优",
        CreatorType.Director => "导演",
        CreatorType.Actor => "演员",
        _ => type.ToString()
    };

    /// <summary>
    /// 获取CreatorType的图标
    /// </summary>
    private string GetCreatorTypeIcon(CreatorType type) => type switch
    {
        CreatorType.Author => Icons.Material.Filled.Create,
        CreatorType.Illustrator => Icons.Material.Filled.Brush,
        CreatorType.Musician => Icons.Material.Filled.MusicNote,
        CreatorType.ScreenWriter => Icons.Material.Filled.Edit,
        CreatorType.VoiceActor => Icons.Material.Filled.RecordVoiceOver,
        CreatorType.Director => Icons.Material.Filled.MovieFilter,
        CreatorType.Actor => Icons.Material.Filled.Person,
        _ => Icons.Material.Filled.Person
    };

    /// <summary>
    /// 获取CreatorType的颜色
    /// </summary>
    private Color GetCreatorTypeColor(CreatorType type) => type switch
    {
        CreatorType.Author => Color.Primary,
        CreatorType.Illustrator => Color.Warning,
        CreatorType.Musician => Color.Success,
        CreatorType.ScreenWriter => Color.Info,
        CreatorType.VoiceActor => Color.Secondary,
        CreatorType.Director => Color.Tertiary,
        CreatorType.Actor => Color.Error,
        _ => Color.Default
    };
}
