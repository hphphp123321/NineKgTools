using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media;
using Serilog;
using X.PagedList;

namespace NineKgTools.Pages.Creators;

public partial class CirclesPage : ComponentBase
{
    [Inject] private CreatorService CreatorService { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    private IPagedList<Circle>? _pagedCircles;
    private int _totalCircleCount;
    private bool _isLoading;
    private string _searchTerm = string.Empty;

    // 分页
    private int _currentPage = 1;
    private int _totalPages;
    private int _pageSize = 24;
    private static readonly int[] AllowedPageSizes = { 12, 24, 48, 96 };

    // 新增 Circle 相关
    private bool _showAddDialog;
    private string _newCircleName = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await LoadCircles();
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task LoadCircles()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            _totalCircleCount = await CreatorService.GetCircleCountAsync();
            _pagedCircles = await CreatorService.GetPagedCirclesAsync(
                _currentPage, _pageSize, _searchTerm);
            _totalPages = _pagedCircles.PageCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载社团列表失败");
            Snackbar.Add("加载社团列表失败，请稍后重试。", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task HandleSearchTermChanged()
    {
        _currentPage = 1;
        await LoadCircles();
    }

    private async Task OnPageChanged(int page)
    {
        if (page == _currentPage) return;
        _currentPage = page;
        await LoadCircles();
    }

    private async Task OnPageSizeChanged(int newSize)
    {
        if (newSize == _pageSize) return;
        _pageSize = newSize;
        _currentPage = 1;
        await LoadCircles();
    }

    private void OpenAddDialog()
    {
        _newCircleName = string.Empty;
        _showAddDialog = true;
    }

    private void CloseAddDialog()
    {
        _showAddDialog = false;
    }

    private async Task CreateCircle()
    {
        if (string.IsNullOrWhiteSpace(_newCircleName))
        {
            Snackbar.Add("请输入社团名称", Severity.Warning);
            return;
        }

        try
        {
            var newCircle = await CreatorService.CreateCircleAsync(_newCircleName);
            Snackbar.Add($"已创建社团：{newCircle.Name}", Severity.Success);

            CloseAddDialog();

            NavigationManager.NavigateTo($"/circle/{newCircle.Id}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建社团失败 Name={Name}", _newCircleName);
            Snackbar.Add("创建社团失败，请稍后重试。", Severity.Error);
        }
    }

    private async Task DeleteCircle(Circle circle)
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "删除社团",
            "删除后该社团与其所有作品的关联会被解除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "删除",
            targetName: circle.Name,
            targetIcon: Icons.Material.Filled.Groups);

        if (confirmed)
        {
            try
            {
                await CreatorService.DeleteCircleAsync(circle.Id);
                Snackbar.Add($"已删除社团：{circle.Name}", Severity.Success);
                await LoadCircles();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除社团失败 CircleId={Id}", circle.Id);
                Snackbar.Add("删除社团失败，请稍后重试。", Severity.Error);
            }
        }
    }

    private void NavigateToCircle(Circle circle)
    {
        NavigationManager.NavigateTo($"/circle/{circle.Id}");
    }
}
