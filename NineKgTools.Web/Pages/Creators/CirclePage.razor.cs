using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Utils;

namespace NineKgTools.Pages.Creators;

public partial class CirclePage : ComponentBase
{
    [Parameter] public int Id { get; set; }

    [Inject] CreatorService CreatorService { get; set; }
    [Inject] ImageService ImageService { get; set; }

    private string _name;
    private List<string> _aliasNames = new();
    private string _description;
    private string _descriptionTranslated;
    private Image _avatar;
    private List<MediaBase> _associatedMedias = new();

    // MediaShownView 组件引用（用于刷新关联作品列表）
    private MediaShownView? _mediaShownView;

    // 初始查询参数（用于MediaShownView）
    private MediaQueryParameters _initialParams = new();

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        await GetCircle();
        _isLoading = false;
    }

    private async Task GetCircle()
    {
        var circle = await CreatorService.GetCircleAsync(Id);
        if (circle == null)
        {
            return;
        }

        _name = circle.Name;
        _aliasNames = new List<string>(circle.AliasNames);
        _description = circle.Description ?? "";
        _descriptionTranslated = circle.DescriptionTranslated ?? "";
        _avatar = circle.Avatar;

        // 加载关联媒体作品
        _associatedMedias = await CreatorService.GetCircleMediasAsync(Id);

        // 设置初始查询参数：筛选该社团的媒体
        _initialParams = new MediaQueryParameters
        {
            CircleId = circle.Id,
            SortOption = MediaSortOption.ReleaseDateDesc
        };
    }

    // AI翻译功能
    private async Task TranslateDescription()
    {
        if (string.IsNullOrEmpty(_description))
        {
            Snackbar.Add("原文描述为空，无法进行翻译", Severity.Warning);
            return;
        }

        try
        {
            _translating = true;

            var translatedText = await OpenaiService.Translate(_description);

            if (!string.IsNullOrEmpty(translatedText))
            {
                _descriptionTranslated = translatedText;
                Snackbar.Add("翻译完成", Severity.Success);
            }
            else
            {
                Snackbar.Add("翻译失败，请稍后重试", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"翻译出错: {ex.Message}", Severity.Error);
        }
        finally
        {
            _translating = false;
        }
    }

    /// <summary>
    /// AI翻译姓名功能
    /// </summary>
    private async Task TranslateName()
    {
        if (string.IsNullOrEmpty(_name))
        {
            Snackbar.Add("原文名称为空，无法进行翻译", Severity.Warning);
            return;
        }

        try
        {
            _translatingName = true;

            var translatedText = await OpenaiService.Translate(_name);

            if (!string.IsNullOrEmpty(translatedText))
            {
                _aliasNames.Add(translatedText);
                Snackbar.Add("名称翻译完成并已添加到别名中", Severity.Success);
            }
            else
            {
                Snackbar.Add("翻译失败，请稍后重试", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"翻译出错: {ex.Message}", Severity.Error);
        }
        finally
        {
            _translatingName = false;
        }
    }

    private async Task OnAliasNamesChanged(List<string> newAliasNames)
    {
        _aliasNames = newAliasNames;
        StateHasChanged();
    }

    private async Task _saveCircle()
    {
        _saveProcessing = true;

        var circle = new Circle
        {
            Id = Id,
            Name = _name,
            AliasNames = _aliasNames,
            Description = _description,
            DescriptionTranslated = _descriptionTranslated,
            Avatar = _avatar
        };

        await CreatorService.FindAndUpdateCircleAsync(circle);
        await Task.Delay(500);

        // 刷新数据
        await GetCircle();

        Snackbar.Add("保存成功", Severity.Success);
        _saveProcessing = false;
    }

    private async Task OpenMediaSelectorDialog()
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Large,
            FullWidth = true
        };

        var parameters = new DialogParameters<MediaSelectorDialog>
        {
            { nameof(MediaSelectorDialog.InitialSelectedMedias), _associatedMedias.ToList() }
        };

        var dialog = await DialogService.ShowAsync<MediaSelectorDialog>("选择关联作品", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is List<MediaBase> selectedMedias)
        {
            _associatedMedias = selectedMedias;
            await CreatorService.UpdateCircleMediasAsync(Id, _associatedMedias.Select(m => m.Id).ToList());
            Snackbar.Add("关联作品已更新", Severity.Success);
            if (_mediaShownView != null)
                await _mediaShownView.Refresh();
        }
    }

    private async Task OpenUploadDialog()
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        var parameters = new DialogParameters<ImageUploadDialog>
        {
            { nameof(ImageUploadDialog.ParentDirName), _name},
        };

        var dialog = await DialogService.ShowAsync<ImageUploadDialog>("上传社团头像", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is Image uploadedImage)
        {
            try
            {
                _avatar = uploadedImage;

                Snackbar.Add("头像已更新，请保存更改", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"处理头像失败: {ex.Message}", Severity.Error);
            }
        }
    }

    // 获取要显示的头像URL的辅助方法
    private string GetAvatarUrl()
    {
        return _avatar?.GetImageUrl() ?? StaticStrings.ImageNotFound;
    }

    // UI状态控制字段
    private bool _saveProcessing = false;
    private bool _isLoading = false;
    private bool _isOriginalExpanded = true;
    private bool _isTranslatedExpanded = false;
    private bool _translating = false;
    private bool _translatingName = false;

    // 监控原始描述面板状态变化
    private void OnOriginalExpandedChanged(bool value)
    {
        _isOriginalExpanded = value;
        if (value && _isTranslatedExpanded)
        {
            _isTranslatedExpanded = false;
        }
    }

    // 监控翻译描述面板状态变化
    private void OnTranslatedExpandedChanged(bool value)
    {
        _isTranslatedExpanded = value;
        if (value && _isOriginalExpanded)
        {
            _isOriginalExpanded = false;
        }
    }


}