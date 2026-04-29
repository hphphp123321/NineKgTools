using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Models.Tags;
using NineKgTools.Core.Services.AI;
using NineKgTools.Core.Services.Favorites;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Images;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Tags;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Medias;

public partial class MediaPage : ComponentBase
{
    [Parameter] public int Id { get; set; }

    /// <summary>
    /// 查询参数 ?edit=true —— 用于外部入口（如"待识别"Tab 的手动添加流程）
    /// 跳转到本页时自动进入编辑模式，免去用户再点一次"编辑"按钮。
    /// 其它值或缺省时不启用。
    /// </summary>
    [SupplyParameterFromQuery(Name = "edit")]
    public bool? Edit { get; set; }

    [CascadingParameter] public bool IsDarkMode { get; set; }

    [Inject] private MediaService MediaService { get; set; }
    [Inject] private ImageService ImageService { get; set; }
    [Inject] private IDialogService DialogService { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private FilesService FileService { get; set; }
    [Inject] private FavoriteService FavoriteService { get; set; }
    [Inject] private OpenaiService OpenaiService { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }
    [Inject] private TagService TagService { get; set; }
    [Inject] private IFileExplorerService FileExplorerService { get; set; }

    [Inject] private IJSRuntime JsRuntime { get; set; }

    private MediaBase? _media;
    private bool _isLoading = true;
    private bool _isEditMode = false;
    private bool _saveProcessing = false;
    private bool _translating = false;
    private int _rating;
    private int _currentId; // 跟踪当前加载的媒体ID

    // 远程访问检测
    private bool IsRemoteAccess => !FileExplorerService.IsLocalAccessSupported;

    // 图片预览对话框相关
    private bool _isPreviewDialogVisible = false;
    private Image _previewImage;

    // 日期选择器相关
    private bool _isDatePickerVisible = false;
    private string _datePickerTitle = string.Empty;
    private DateTime? _selectedDate = null;
    private string _dateFieldName = string.Empty;

    // 添加链接对话框相关
    private bool _isAddLinkDialogVisible = false;
    private string _newLinkUrl = string.Empty;

    // 折叠面板状态（默认全部展开）
    private bool _mediaDetailsExpanded = true;
    private bool _mediaImagesExpanded = true;
    private bool _descriptionExpanded = true;
    private bool _relatedMediaExpanded = true;

    // DescriptionEditor组件引用
    private DescriptionEditor? _descriptionEditor;


    // 标签颜色映射字典 - 根据标签类别分配不同颜色
    private readonly Dictionary<string, Color> _tagColorMap = new()
    {
        { "角色", Color.Primary },
        { "人物", Color.Primary },
        { "类型", Color.Secondary },
        { "题材", Color.Secondary },
        { "风格", Color.Warning },
        { "场景", Color.Info },
        { "内容", Color.Success },
        { "形式", Color.Tertiary }
    };

    protected override async Task OnInitializedAsync()
    {
        _currentId = Id;
        _isLoading = true;
        await LoadMedia();
        _isLoading = false;

        // 若通过 ?edit=true 进入（如手动添加媒体后的跳转），自动进入编辑模式
        if (Edit == true && _media != null)
            _isEditMode = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        // 检测路由参数变化，重新加载数据
        if (Id != _currentId && _currentId != 0)
        {
            _currentId = Id;
            _isLoading = true;
            StateHasChanged();
            await LoadMedia();
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadMedia()
    {
        try
        {
            // 1. 首先获取基本的媒体信息
            _media = await MediaService.GetMediaAsync(Id);
            
            if (_media == null) return;
            
            // 2. 然后根据媒体类型获取具体的详细信息
            _media = _media.Category.TopCategory switch
            {
                TopCategory.Video => await LoadVideoMediaDetails(_media as VideoMedia ?? new VideoMedia(_media)),
                TopCategory.Audio => await LoadAudioMediaDetails(_media as AudioMedia ?? new AudioMedia(_media)),
                TopCategory.Game => await LoadGameMediaDetails(_media as GameMedia ?? new GameMedia(_media)),
                TopCategory.Picture =>
                    await LoadPictureMediaDetails(_media as PictureMedia ?? new PictureMedia(_media)),
                TopCategory.Text => await LoadTextMediaDetails(_media as TextMedia ?? new TextMedia(_media)),
                _ => _media
            };

            _rating = (int)_media.Rating;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"载入媒体信息时出错: {ex.Message}", Severity.Error);
        }
    }

    #region 获取特定媒体类型的详细信息

    private async Task<VideoMedia> LoadVideoMediaDetails(VideoMedia videoMedia)
    {
        try
        {
            // 从MediaService中请求完整的VideoMedia数据
            return await Task.FromResult(videoMedia);
        }
        catch (Exception)
        {
            // 如果出错，我们至少返回已有的基本信息
            return videoMedia;
        }
    }

    private async Task<AudioMedia> LoadAudioMediaDetails(AudioMedia audioMedia)
    {
        try
        {
            return await Task.FromResult(audioMedia);
        }
        catch (Exception)
        {
            return audioMedia;
        }
    }

    private async Task<GameMedia> LoadGameMediaDetails(GameMedia gameMedia)
    {
        try
        {
            return await Task.FromResult(gameMedia);
        }
        catch (Exception)
        {
            return gameMedia;
        }
    }

    private async Task<PictureMedia> LoadPictureMediaDetails(PictureMedia pictureMedia)
    {
        try
        {
            return await Task.FromResult(pictureMedia);
        }
        catch (Exception)
        {
            return pictureMedia;
        }
    }

    private async Task<TextMedia> LoadTextMediaDetails(TextMedia textMedia)
    {
        try
        {
            return await Task.FromResult(textMedia);
        }
        catch (Exception)
        {
            return textMedia;
        }
    }

    #endregion

    private async Task SaveMedia()
    {
        if (_media == null) return;

        _saveProcessing = true;
        
        try
        {
            // 将评分更新回 Media 对象
            _media.Rating = _rating;

            // 使用UpdateMediaAsync方法，它会处理分类变更情况
            var updatedMedia = await MediaService.UpdateMediaAsync(_media);
            
            if (updatedMedia != null)
            {
                _media = updatedMedia;
                Snackbar.Add("保存成功", Severity.Success);
            }
            else
            {
                Snackbar.Add("保存失败，服务端未返回更新数据", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saveProcessing = false;
            // 重新加载媒体信息
            await LoadMedia();
            _isEditMode = false;
            _descriptionEditor?.ExitEditMode();
        }
    }

    private async Task TranslateSummary()
    {
        if (_translating) return;
        if (_media == null || string.IsNullOrEmpty(_media.Summary))
        {
            Snackbar.Add("简介为空，无法翻译", Severity.Warning);
            return;
        }

        try
        {
            _translating = true;
            
            var translatedText = await OpenaiService.Translate(_media.Summary);
            
            if (!string.IsNullOrEmpty(translatedText))
            {
                _media.SummaryTranslated = translatedText;
                Snackbar.Add("简介翻译完成", Severity.Success);
            }
            else
            {
                Snackbar.Add("简介翻译失败，未获取到翻译结果", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"简介翻译失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _translating = false;
        }
    }

    private string GetPosterUrl()
    {
        return _media?.Poster?.GetImageUrl() ?? StaticStrings.ImageNotFound;
    }

    private string GetFormattedSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private bool FileExists(string path)
    {
        try
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private string GetLinkDisplayName(Uri link)
    {
        if (link == null) return string.Empty;
        
        // 提取域名的主要部分，去除www.和顶级域名
        string host = link.Host.ToLower();
        
        // 移除开头的"www."
        if (host.StartsWith("www."))
        {
            host = host.Substring(4);
        }
        
        // 移除顶级域名(如.com, .org等)
        int lastDotIndex = host.LastIndexOf('.');
        if (lastDotIndex > 0)
        {
            // 检查是否有二级域名，如.co.jp
            int secondLastDotIndex = host.LastIndexOf('.', lastDotIndex - 1);
            if (secondLastDotIndex > 0 && lastDotIndex - secondLastDotIndex <= 4) // .co, .com.cn 等
            {
                host = host.Substring(0, secondLastDotIndex);
            }
            else
            {
                host = host.Substring(0, lastDotIndex);
            }
        }
        
        // 首字母大写以美化显示
        if (host.Length > 0)
        {
            host = char.ToUpper(host[0]) + host.Substring(1);
        }
        
        return host;
    }

    private string GetLinkFavicon(Uri link)
    {
        if (link == null) return Icons.Material.Filled.Link;
        
        string host = link.Host.ToLower();
        
        // 根据域名返回适当的图标
        return host switch
        {
            var h when h.Contains("dlsite") => Icons.Material.Filled.ShoppingCart,
            var h when h.Contains("twitter") || h.Contains("x.com") => Icons.Custom.Brands.Twitter,
            var h when h.Contains("pixiv") => Icons.Material.Filled.Draw,
            var h when h.Contains("youtube") => Icons.Custom.Brands.YouTube,
            var h when h.Contains("amazon") => Icons.Material.Filled.LocalMall,
            var h when h.Contains("fanbox") => Icons.Material.Filled.LocalActivity,
            var h when h.Contains("steam") => Icons.Material.Filled.SportsEsports,
            var h when h.Contains("wikipedia") => Icons.Material.Filled.MenuBook,
            var h when h.Contains("github") => Icons.Custom.Brands.GitHub,
            var h when h.Contains("patreon") => Icons.Material.Filled.Loyalty,
            var h when h.Contains("instagram") => Icons.Custom.Brands.Instagram,
            var h when h.Contains("facebook") => Icons.Custom.Brands.Facebook,
            var h when h.Contains("bilibili") => Icons.Material.Filled.LiveTv,
            var h when h.Contains("getchu") => Icons.Material.Filled.Style,
            var h when h.Contains("bangumi") => Icons.Material.Filled.Star,
            _ => Icons.Material.Filled.Link
        };
    }

    private async Task OpenUploadPosterDialog()
    {
        if (_media == null) return;
        
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };
        
        var parameters = new DialogParameters<ImageUploadDialog>
        {
            { nameof(ImageUploadDialog.ParentDirName), _media.Title },
        };
        
        var dialog = await DialogService.ShowAsync<ImageUploadDialog>("上传海报", parameters, options);
        var result = await dialog.Result;
        
        if (!result.Canceled && result.Data is Image uploadedImage)
        {
            try
            {
                _media.Poster = uploadedImage;
                Snackbar.Add("海报已更新，请保存更改", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"处理海报失败: {ex.Message}", Severity.Error);
            }
        }
    }
    
    private async Task OpenUploadMediaImageDialog()
    {
        if (_media == null) return;
        
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };
        
        var parameters = new DialogParameters<ImageUploadDialog>
        {
            { nameof(ImageUploadDialog.ParentDirName), _media.Title },
        };
        
        var dialog = await DialogService.ShowAsync<ImageUploadDialog>("添加媒体图片", parameters, options);
        var result = await dialog.Result;
        
        if (!result.Canceled && result.Data is Image uploadedImage)
        {
            try
            {
                // 添加到媒体图片列表
                _media.Pictures.Add(uploadedImage);
                Snackbar.Add("图片已添加，请保存更改", Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"处理图片失败: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task CopySourceFilepath()
    {
        if (_media?.Source == null)
        {
            Snackbar.Add("无法复制源文件地址，源文件信息不存在", Severity.Warning);
            return;
        }

        var sourcePath = _media.Source.FullPath;
        if (string.IsNullOrEmpty(sourcePath))
        {
            Snackbar.Add("源文件路径为空", Severity.Warning);
            return;
        }
        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", sourcePath);
            Snackbar.Add("源文件路径已复制到剪贴板", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"复制失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打开入口文件
    /// </summary>
    private async Task OpenEntryFile()
    {
        if (_media?.Source == null || string.IsNullOrEmpty(_media.Source.EntryFilePath))
        {
            Snackbar.Add("入口文件未设置", Severity.Warning);
            return;
        }

        try
        {
            var result = _media.Category.TopCategory == TopCategory.Game
                ? await FileExplorerService.RunExecutableAsync(_media.Source.EntryFilePath)
                : await FileExplorerService.OpenFileWithDefaultAppAsync(_media.Source.EntryFilePath);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? "打开文件失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打开失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打开媒体源所在目录
    /// </summary>
    private async Task OpenSourceLocation()
    {
        if (_media?.Source == null)
        {
            Snackbar.Add("媒体源不存在", Severity.Warning);
            return;
        }

        try
        {
            var result = _media.Source.IsFolder
                ? await FileExplorerService.OpenFolderInExplorerAsync(_media.Source.FullPath)
                : await FileExplorerService.ShowFileInExplorerAsync(_media.Source.FullPath);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? "打开文件位置失败", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"打开失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 检查是否有入口文件
    /// </summary>
    private bool HasEntryFile => _media?.Source != null && !string.IsNullOrEmpty(_media.Source.EntryFilePath);

    private bool _deleteProcessing = false;

    private async Task DeleteMedia()
    {
        if (_media == null || _deleteProcessing) return;
        _deleteProcessing = true;

        try
        {
            var confirmed = await NineKgConfirmDialog.ShowAsync(
                DialogService,
                "删除媒体",
                "删除后媒体信息将从数据库移除，文件本身不受影响。",
                intent: ConfirmIntent.Destructive,
                confirmText: "删除",
                targetName: _media.Title,
                targetIcon: Icons.Material.Filled.MovieFilter,
                warningLine: "删除后无法恢复");

            if (confirmed)
            {
                try
                {
                    await MediaService.RemoveMediaAsync(_media.Source);
                    Snackbar.Add("媒体已删除", Severity.Success);
                    NavigationManager.NavigateTo("/");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "删除媒体失败 MediaId={Id}", _media.Id);
                    Snackbar.Add("删除失败，请稍后重试。", Severity.Error);
                }
            }
        }
        finally
        {
            _deleteProcessing = false;
        }
    }

    // 折叠面板键盘支持（Enter/Space触发展开/折叠）
    private void OnCollapseKeyDown(KeyboardEventArgs e, string panel)
    {
        if (e.Key is "Enter" or " ")
        {
            switch (panel)
            {
                case "details": _mediaDetailsExpanded = !_mediaDetailsExpanded; break;
                case "images": _mediaImagesExpanded = !_mediaImagesExpanded; break;
                case "description": _descriptionExpanded = !_descriptionExpanded; break;
                case "related": _relatedMediaExpanded = !_relatedMediaExpanded; break;
            }
        }
    }

    private void OpenImagePreview(Image image)
    {
        _previewImage = image;
        _isPreviewDialogVisible = true;
    }
    
    private void CloseImagePreview()
    {
        _isPreviewDialogVisible = false;
    }
    
    // 根据标签获取颜色
    private Color GetTagColor(string tagName)
    {
        // 尝试根据标签内容分配合适的颜色
        foreach (var category in _tagColorMap.Keys)
        {
            if (tagName.Contains(category))
            {
                return _tagColorMap[category];
            }
        }
        
        // 如果没有匹配的类别，使用计算的颜色
        return CalculateTagColor(tagName);
    }
    
    // 计算标签的颜色（基于确定性哈希，不同会话结果一致）
    private Color CalculateTagColor(string tagName)
    {
        if (string.IsNullOrEmpty(tagName))
            return Color.Default;

        // 使用确定性的字符求和，避免GetHashCode在不同运行时产生不同结果
        int hash = 0;
        foreach (var c in tagName)
        {
            hash = hash * 31 + c;
        }

        // 颜色列表
        var colors = new[] {
            Color.Primary, Color.Secondary, Color.Info,
            Color.Success, Color.Warning, Color.Tertiary };

        // 使用哈希值选择一个颜色
        return colors[Math.Abs(hash) % colors.Length];
    }

    #region UI Helpers
    /// <summary>
    /// 获取平台对应的Font Awesome图标类名
    /// </summary>
    private string GetPlatformIconClass(Platform platform) => platform switch
    {
        Platform.Windows => "fa-windows",
        Platform.Android => "fa-android",
        Platform.iOS => "fa-apple",
        Platform.Mac => "fa-apple",
        Platform.Linux => "fa-linux",
        _ => "fa-gamepad"
    };


    private string GetMediaIcon(TopCategory category) => category switch
    {
        TopCategory.Video => Icons.Material.Filled.SmartDisplay,
        TopCategory.Audio => Icons.Material.Filled.Mic,
        TopCategory.Picture => Icons.Material.Filled.Image,
        TopCategory.Text => Icons.Material.Filled.MenuBook,
        TopCategory.Game => Icons.Material.Filled.SportsEsports,
        _ => Icons.Material.Filled.Help
    };

    private Color GetMediaColor(TopCategory category) => MediaUIHelper.GetMediaColor(category);
    
    private string GetMediaTypeName(TopCategory category) => category switch
    {
        TopCategory.Video => "视频",
        TopCategory.Audio => "音频",
        TopCategory.Picture => "图片",
        TopCategory.Text => "文本",
        TopCategory.Game => "游戏",
        _ => "媒体"
    };

    private string GetMediaColorClass(TopCategory category) => category switch
    {
        TopCategory.Video => "primary",
        TopCategory.Audio => "secondary",
        TopCategory.Picture => "warning",
        TopCategory.Text => "tertiary",
        TopCategory.Game => "info",
        _ => "primary"
    };

    private string GetHeaderCardClass(TopCategory category)
    {
        return "card-base card-header mb-4";
    }

    #endregion

    // 格式化时长显示
    private string FormatDuration(int seconds)
    {
        if (seconds <= 0) return "未知";
        
        var timeSpan = TimeSpan.FromSeconds(seconds);
        
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}小时{timeSpan.Minutes}分钟";
        }
        
        return $"{timeSpan.Minutes}分钟{timeSpan.Seconds}秒";
    }

    // 添加一个翻译详情的方法
    private async Task TranslateDescription()
    {
        if (_translating) return;
        if (_media == null || string.IsNullOrEmpty(_media.Description))
        {
            Snackbar.Add("描述为空，无法翻译", Severity.Warning);
            return;
        }

        try
        {
            var content = _media.Description;
            // 提取纯文本，去除HTML标签
            string plainText = System.Text.RegularExpressions.Regex.Replace(content, "<.*?>", " ");
            
            // 如果超过一定长度，截取前一部分进行翻译
            if (plainText.Length > 8000)
            {
                var totalLength = plainText.Length;
                plainText = plainText.Substring(0, 4000) + "...";
                Snackbar.Add($"原文共{totalLength}字，仅翻译前4000字", Severity.Info);
            }
            
            _translating = true;
            
            var translatedText = await OpenaiService.Translate(plainText);
            
            if (!string.IsNullOrEmpty(translatedText))
            {
                // 设置翻译后的内容
                _media.DescriptionTranslated = "<div class=\"translated-content\">" + translatedText + "</div>";
                Snackbar.Add("描述翻译完成", Severity.Success);
            }
            else
            {
                Snackbar.Add("描述翻译失败，未获取到翻译结果", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"描述翻译失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            _translating = false;
            StateHasChanged();
        }
    }

    private void OpenDatePicker(string fieldName)
    {
        _dateFieldName = fieldName;
        
        if (fieldName == "StoreDate")
        {
            _datePickerTitle = "修改入库日期";
            _selectedDate = _media.StoreDate;
        }
        else if (fieldName == "ReleaseDate")
        {
            _datePickerTitle = "修改发行日期";
            _selectedDate = _media.ReleaseDate;
        }
        
        _isDatePickerVisible = true;
    }

    private void CloseDatePicker()
    {
        _isDatePickerVisible = false;
    }

    private void SaveSelectedDate()
    {
        if (_dateFieldName == "StoreDate")
        {
            _media.StoreDate = _selectedDate;
        }
        else if (_dateFieldName == "ReleaseDate")
        {
            _media.ReleaseDate = _selectedDate;
        }
        
        CloseDatePicker();
    }

    // 进入编辑模式
    private void EnterEditMode()
    {
        _isEditMode = true;
    }

    // 取消编辑模式，丢弃所有未保存的变更
    private async Task CancelEditMode()
    {
        _isEditMode = false;
        _descriptionEditor?.ExitEditMode();
        await LoadMedia();
    }

    // 刷新媒体数据
    private async Task RefreshMedia()
    {
        // 当收藏状态改变时刷新媒体数据
        if (_media != null)
        {
            _media = await MediaService.GetMediaAsync(_media.Id);
        }
    }

    // 分类选择相关
    private async Task CategoryChanged(Category newCategory)
    {
        if (_media != null && newCategory != null)
        {
            var oldTopCategory = _media.Category.TopCategory;
            var newTopCategory = newCategory.TopCategory;
            
            // 更新分类
            _media.Category = newCategory;
            Snackbar.Add($"分类已更改为: {newCategory.Name}", Severity.Info);
            
            // 如果媒体类型更改，可能需要重新加载特定类型的详细信息
            if (oldTopCategory != newTopCategory)
            {
                _media = newCategory.TopCategory switch
                {
                    TopCategory.Video => _media is VideoMedia vm ? vm : new VideoMedia(_media),
                    TopCategory.Audio => _media is AudioMedia am ? am : new AudioMedia(_media),
                    TopCategory.Game => _media is GameMedia gm ? gm : new GameMedia(_media),
                    TopCategory.Picture => _media is PictureMedia pm ? pm : new PictureMedia(_media),
                    TopCategory.Text => _media is TextMedia tm ? tm : new TextMedia(_media),
                    _ => _media
                };
                
                Snackbar.Add($"媒体类型已转换为: {GetMediaTypeName(newTopCategory)}", Severity.Warning);
                Snackbar.Add("需要保存更改才能完成转换", Severity.Info);
                
                StateHasChanged();
            }
        }
    }

    // 打开添加链接对话框
    private void OpenAddLinkDialog()
    {
        _newLinkUrl = string.Empty;
        _isAddLinkDialogVisible = true;
    }
    
    // 关闭添加链接对话框
    private void CloseAddLinkDialog()
    {
        _isAddLinkDialogVisible = false;
    }
    
    // 添加新链接
    private void AddNewLink()
    {
        if (_media == null || string.IsNullOrWhiteSpace(_newLinkUrl)) return;
        
        try
        {
            var uri = new Uri(_newLinkUrl);
            
            // 检查是否已存在相同链接
            if (_media.Links.Any(l => l.ToString() == uri.ToString()))
            {
                Snackbar.Add("该链接已存在", Severity.Warning);
                return;
            }
            
            _media.Links.Add(uri);
            Snackbar.Add("链接已添加", Severity.Success);
            CloseAddLinkDialog();
        }
        catch
        {
            Snackbar.Add("无效的URL格式", Severity.Error);
        }
    }
    
    // 移除链接
    private void RemoveLink(Uri link)
    {
        if (_media == null || link == null) return;
        
        _media.Links.Remove(link);
        Snackbar.Add("链接已删除", Severity.Info);
    }
    
    // 验证是否为有效URL
    private bool IsValidUrl(string url)
    {
        return !string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out Uri? _);
    }

    private Task OnAliasTitlesChanged(List<string> newAliasTitles)
    {
        if (_media != null)
        {
            _media.AliasTitles = newAliasTitles;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    // 标签管理相关方法
    private Task OnTagsChanged(List<Tag> newTags)
    {
        if (_media != null)
        {
            _media.Tags = newTags;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    // 扩展信息管理相关方法
    private Task OnInfosChanged(Dictionary<string, string> newInfos)
    {
        if (_media != null)
        {
            _media.Infos = newInfos;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    // 描述管理相关方法
    private Task OnDescriptionChanged(string newDescription)
    {
        if (_media != null)
        {
            _media.Description = newDescription;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    private Task OnDescriptionTranslatedChanged(string newDescriptionTranslated)
    {
        if (_media != null)
        {
            _media.DescriptionTranslated = newDescriptionTranslated;
            StateHasChanged();
        }
        return Task.CompletedTask;
    }

    #region 相关媒体管理

    /// <summary>
    /// 打开媒体选择对话框
    /// </summary>
    private async Task OpenMediaSelectorDialog()
    {
        if (_media == null) return;

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        var parameters = new DialogParameters<MediaSelectorDialog>
        {
            { nameof(MediaSelectorDialog.InitialSelectedMedias), _media.RelatedMedias.ToList() },
            { nameof(MediaSelectorDialog.ExcludeMediaId), _media.Id }
        };

        var dialog = await DialogService.ShowAsync<MediaSelectorDialog>("选择相关媒体", parameters, options);
        var result = await dialog.Result;

        if (!result.Canceled && result.Data is List<MediaBase> selectedMedias)
        {
            await ProcessRelatedMediasSelection(selectedMedias);
        }
    }

    /// <summary>
    /// 处理相关媒体选择结果（双向关联）
    /// </summary>
    private async Task ProcessRelatedMediasSelection(List<MediaBase> selectedMedias)
    {
        if (_media == null) return;

        try
        {
            // 找出新增的媒体（在选择中但不在当前列表中）
            var currentIds = _media.RelatedMedias.Select(m => m.Id).ToHashSet();
            var selectedIds = selectedMedias.Select(m => m.Id).ToHashSet();

            var toAdd = selectedMedias.Where(m => !currentIds.Contains(m.Id)).ToList();
            var toRemove = _media.RelatedMedias.Where(m => !selectedIds.Contains(m.Id)).ToList();

            // 添加新的关联（双向）
            foreach (var media in toAdd)
            {
                await MediaService.AddRelatedMediaAsync(_media.Id, media.Id);
            }

            // 移除取消的关联（双向）
            foreach (var media in toRemove)
            {
                await MediaService.RemoveRelatedMediaAsync(_media.Id, media.Id);
            }

            // 重新加载媒体数据
            await LoadMedia();

            if (toAdd.Count > 0 || toRemove.Count > 0)
            {
                Snackbar.Add($"相关媒体已更新（添加 {toAdd.Count} 个，移除 {toRemove.Count} 个）", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"更新相关媒体失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 确认移除相关媒体
    /// </summary>
    private async Task ConfirmRemoveRelatedMedia(MediaBase relatedMedia)
    {
        var confirmed = await NineKgConfirmDialog.ShowAsync(
            DialogService,
            "移除相关媒体",
            "两个媒体之间的关联将被解除（双向），媒体本身不会被删除。",
            intent: ConfirmIntent.Destructive,
            confirmText: "移除",
            targetName: relatedMedia.Title,
            targetIcon: Icons.Material.Filled.LinkOff,
            warningLine: "可随时重新关联");

        if (confirmed)
        {
            await RemoveRelatedMedia(relatedMedia);
        }
    }

    /// <summary>
    /// 移除相关媒体（双向）
    /// </summary>
    private async Task RemoveRelatedMedia(MediaBase relatedMedia)
    {
        if (_media == null) return;

        try
        {
            await MediaService.RemoveRelatedMediaAsync(_media.Id, relatedMedia.Id);

            // 重新加载媒体数据
            await LoadMedia();

            Snackbar.Add($"已移除相关媒体: {relatedMedia.Title}", Severity.Info);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"移除相关媒体失败: {ex.Message}", Severity.Error);
        }
    }

    #endregion
}