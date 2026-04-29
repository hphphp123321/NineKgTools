using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using Serilog;

namespace NineKgTools.Pages.Tags;

public partial class TagPage : ComponentBase
{
    [Parameter] public int TagId { get; set; }

    [Inject] private TagService TagService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private NavigationManager Navigation { get; set; } = null!;

    private Tag? _tag;
    private bool _isLoading = true;
    private bool _saveProcessing = false;

    // 初始查询参数（用于MediaShownView）
    private MediaQueryParameters _initialParams = new();

    protected override async Task OnParametersSetAsync()
    {
        await LoadTagData();
    }

    /// <summary>
    /// 加载标签数据
    /// </summary>
    private async Task LoadTagData()
    {
        try
        {
            _isLoading = true;
            StateHasChanged();

            // 使用 Task.Run 避免阻塞 UI 线程
            _tag = await Task.Run(() => TagService.GetTagById(TagId));

            if (_tag == null)
            {
                Log.Warning("未找到ID为 {TagId} 的标签", TagId);
            }
            else
            {
                // 设置初始查询参数：筛选该标签的媒体
                _initialParams = new MediaQueryParameters
                {
                    TagNames = new List<string> { _tag.Name },
                    SortOption = MediaSortOption.StoreDateDesc
                };
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载标签 {TagId} 数据时发生错误", TagId);
            Snackbar.Add("加载标签数据失败", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 保存标签修改
    /// </summary>
    private async Task SaveTag()
    {
        if (_tag == null) return;

        _saveProcessing = true;

        try
        {
            // 验证标签名称不能为空
            if (string.IsNullOrWhiteSpace(_tag.Name))
            {
                Snackbar.Add("标签名称不能为空", Severity.Warning);
                return;
            }

            // 更新标签信息
            await TagService.UpdateTagAsync(_tag);

            Snackbar.Add("标签保存成功", Severity.Success);
            Log.Information("标签 {TagName} 保存成功", _tag.Name);

            // 更新查询参数
            _initialParams = new MediaQueryParameters
            {
                TagNames = new List<string> { _tag.Name },
                SortOption = MediaSortOption.StoreDateDesc
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存标签 {TagId} 时发生错误", _tag.Id);
            Snackbar.Add("保存标签失败", Severity.Error);
        }
        finally
        {
            _saveProcessing = false;
            StateHasChanged();
        }
    }
}

