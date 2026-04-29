using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Utils;

namespace NineKgTools.Pages.Creators;

public partial class CreatorPage : ComponentBase
{
    [Parameter] public int Id { get; set; }

    [Inject] CreatorService CreatorService { get; set; }
    [Inject] ImageService ImageService { get; set; }
    [Inject] ISnackbar Snackbar { get; set; }
    [Inject] IDialogService DialogService { get; set; }
    [Inject] OpenaiService OpenaiService { get; set; }
    [Inject] Config AppConfig { get; set; }

    private string _name = string.Empty;
    private List<string> _aliasNames = new();
    private string _description = string.Empty;
    private string _descriptionTranslated = string.Empty;
    private List<CreatorType> _types = new();
    private Image? _avatar;
    private List<MediaBase> _associatedMedias = new();

    // MediaShownView 组件引用（用于刷新关联作品列表）
    private MediaShownView? _mediaShownView;

    // 初始查询参数（用于MediaShownView）
    private MediaQueryParameters _initialParams = new();

    // UI状态控制
    private bool _saveProcessing = false;
    private bool _isLoading = false;
    private bool _isOriginalExpanded = true;
    private bool _isTranslatedExpanded = false;
    private bool _translating = false;
    private bool _translatingName = false;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        await GetCreator();
        _isLoading = false;
    }

    /// <summary>
    /// 获取Creator信息
    /// </summary>
    private async Task GetCreator()
    {
        var creator = await CreatorService.GetCreatorAsync(Id);
        if (creator == null)
        {
            return;
        }

        _name = creator.Name;
        _aliasNames = new List<string>(creator.AliasNames);
        _description = creator.Description ?? string.Empty;
        _descriptionTranslated = creator.DescriptionTranslated ?? string.Empty;
        _types = creator.Types;
        _avatar = creator.Avatar;

        // 加载关联媒体作品
        _associatedMedias = await CreatorService.GetCreatorMediasAsync(Id);

        // 设置初始查询参数：筛选该创作者的媒体
        // 注意：这需要MediaFilterSpecification中有正确的CreatorId筛选逻辑
        _initialParams = new MediaQueryParameters
        {
            CreatorId = creator.Id,
            SortOption = MediaSortOption.ReleaseDateDesc
        };
    }

    /// <summary>
    /// AI翻译描述功能
    /// </summary>
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
                Snackbar.Add("描述翻译完成", Severity.Success);
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
            Snackbar.Add("原文姓名为空，无法进行翻译", Severity.Warning);
            return;
        }

        try
        {
            _translatingName = true;

            var translatedText = await OpenaiService.Translate(_name);

            if (!string.IsNullOrEmpty(translatedText))
            {
                _aliasNames.Add(translatedText);
                Snackbar.Add("姓名翻译完成并已添加到别名中", Severity.Success);
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

    /// <summary>
    /// 保存Creator信息
    /// </summary>
    private async Task SaveCreator()
    {
        _saveProcessing = true;

        var creator = new Creator
        {
            Id = Id,
            Name = _name,
            AliasNames = _aliasNames,
            Description = _description,
            DescriptionTranslated = _descriptionTranslated,
            Types = _types,
            Avatar = _avatar
        };

        await CreatorService.FindAndUpdateCreatorAsync(creator);
        await Task.Delay(500);

        // 刷新数据
        await GetCreator();

        Snackbar.Add("保存成功", Severity.Success);
        _saveProcessing = false;
    }

    /// <summary>
    /// 打开媒体选择对话框，管理关联作品
    /// </summary>
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
            await CreatorService.UpdateCreatorMediasAsync(Id, _associatedMedias.Select(m => m.Id).ToList());
            Snackbar.Add("关联作品已更新", Severity.Success);
            if (_mediaShownView != null)
                await _mediaShownView.Refresh();
        }
    }

    /// <summary>
    /// 打开上传头像对话框
    /// </summary>
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

        var dialog = await DialogService.ShowAsync<ImageUploadDialog>("上传人物头像", parameters, options);
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

    /// <summary>
    /// 获取要显示的头像URL的辅助方法
    /// </summary>
    private string GetAvatarUrl()
    {
        return _avatar?.GetImageUrl() ?? StaticStrings.ImageNotFound;
    }

    /// <summary>
    /// 监控原始描述面板状态变化
    /// </summary>
    private void OnOriginalExpandedChanged(bool value)
    {
        _isOriginalExpanded = value;
        if (value && _isTranslatedExpanded)
        {
            _isTranslatedExpanded = false;
        }
    }

    /// <summary>
    /// 监控翻译描述面板状态变化
    /// </summary>
    private void OnTranslatedExpandedChanged(bool value)
    {
        _isTranslatedExpanded = value;
        if (value && _isOriginalExpanded)
        {
            _isOriginalExpanded = false;
        }
    }

    private void RemoveType(CreatorType type)
    {
        _types.Remove(type);
    }

    private void AddType(CreatorType type)
    {
        if (!_types.Contains(type))
            _types.Add(type);
    }

    private IEnumerable<CreatorType> GetAvailableTypes()
    {
        return Enum.GetValues<CreatorType>().Where(t => !_types.Contains(t));
    }

    /// <summary>
    /// 获取CreatorType的中文显示名称
    /// </summary>
    private string GetCreatorTypeDisplayName(CreatorType type)
    {
        return type switch
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
    }

    /// <summary>
    /// 获取CreatorType的图标
    /// </summary>
    private string GetCreatorTypeIcon(CreatorType type)
    {
        return type switch
        {
            CreatorType.Author => Icons.Material.Filled.Person,
            CreatorType.Illustrator => Icons.Material.Filled.Brush,
            CreatorType.Musician => Icons.Material.Filled.MusicNote,
            CreatorType.ScreenWriter => Icons.Material.Filled.Edit,
            CreatorType.VoiceActor => Icons.Material.Filled.RecordVoiceOver,
            CreatorType.Director => Icons.Material.Filled.MovieFilter,
            CreatorType.Actor => Icons.Material.Filled.TheaterComedy,
            _ => Icons.Material.Filled.Person
        };
    }

    /// <summary>
    /// 获取CreatorType的颜色
    /// </summary>
    private Color GetCreatorTypeColor(CreatorType type)
    {
        return type switch
        {
            CreatorType.Author => Color.Primary,
            CreatorType.Illustrator => Color.Secondary,
            CreatorType.Musician => Color.Tertiary,
            CreatorType.ScreenWriter => Color.Info,
            CreatorType.VoiceActor => Color.Success,
            CreatorType.Director => Color.Warning,
            CreatorType.Actor => Color.Error,
            _ => Color.Default
        };
    }


}