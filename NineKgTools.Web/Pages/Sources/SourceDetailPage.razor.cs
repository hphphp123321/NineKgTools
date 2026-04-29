using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using NineKgTools.Components.Medias;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Media;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Sources;

public partial class SourceDetailPage : ComponentBase, IDisposable
{
    [Parameter]
    public int Id { get; set; }

    [Inject] private MediaDbContext DbContext { get; set; } = default!;
    [Inject] private MediaService MediaService { get; set; } = default!;
    [Inject] private FilesService FilesService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IFileExplorerService FileExplorerService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private Config Config { get; set; } = default!;

    private MediaSource? _source;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _errorMessage;

    // 入口文件相关
    private List<string> _relevantFiles = new();
    private string? _selectedEntryFile;
    private bool _fileExists;

    // 远程访问检测
    private bool IsRemoteAccess => !FileExplorerService.IsLocalAccessSupported;

    protected override async Task OnInitializedAsync()
    {
        await LoadSourceAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_source?.Id != Id)
        {
            await LoadSourceAsync();
        }
    }

    /// <summary>
    /// 加载媒体源数据
    /// </summary>
    private async Task LoadSourceAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            _source = await DbContext.MediaSources
                .Include(s => s.MediaBase)
                .ThenInclude(m => m!.Poster)
                .FirstOrDefaultAsync(s => s.Id == Id);

            if (_source == null)
            {
                _errorMessage = "媒体源不存在";
            }
            else
            {
                _selectedEntryFile = _source.EntryFilePath;
                _fileExists = !_source.IsFolder && File.Exists(_source.FullPath);
                LoadRelevantFiles();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载媒体源失败: Id={Id}", Id);
            _errorMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>
    /// 加载相关文件列表（根据类别筛选）
    /// </summary>
    private void LoadRelevantFiles()
    {
        if (_source == null) return;

        try
        {
            if (!_source.IsFolder)
            {
                _relevantFiles = new List<string> { _source.FullPath };
                return;
            }

            if (!Directory.Exists(_source.FullPath))
            {
                _relevantFiles = new List<string>();
                return;
            }

            var extensions = TopCategoryExtensions.GetExtensions(_source.PossibleTopCategory);
            _relevantFiles = Directory.GetFiles(_source.FullPath, "*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderBy(f => f)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载相关文件失败: {Path}", _source.FullPath);
            _relevantFiles = new List<string>();
        }
    }

    /// <summary>
    /// 修改媒体源类别
    /// </summary>
    private async Task HandleChangeCategoryAsync(TopCategory newCategory)
    {
        if (_source == null) return;

        try
        {
            // 使用反射设置 protected 属性
            var prop = typeof(MediaSource).GetProperty("PossibleTopCategory");
            prop?.SetValue(_source, newCategory);

            await DbContext.SaveChangesAsync();

            // 刷新相关文件列表
            LoadRelevantFiles();

            Snackbar.Add("类型已更新", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "修改类别失败");
            Snackbar.Add($"修改失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 选择入口文件
    /// </summary>
    private async Task HandleSelectEntryFileAsync(string filePath)
    {
        if (_source == null) return;

        _selectedEntryFile = filePath;
        _source.EntryFilePath = filePath;

        try
        {
            await DbContext.SaveChangesAsync();
            Snackbar.Add("入口文件已保存", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存入口文件失败");
            Snackbar.Add($"保存失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打开/运行入口文件
    /// </summary>
    private async Task HandleOpenEntryFileAsync()
    {
        if (string.IsNullOrEmpty(_source?.EntryFilePath))
        {
            Snackbar.Add("请先选择入口文件", Severity.Warning);
            return;
        }

        if (IsRemoteAccess)
        {
            Snackbar.Add("远程访问时无法打开本地文件", Severity.Warning);
            return;
        }

        try
        {
            var result = _source.PossibleTopCategory == TopCategory.Game
                ? await FileExplorerService.RunExecutableAsync(_source.EntryFilePath)
                : await FileExplorerService.OpenFileWithDefaultAppAsync(_source.EntryFilePath);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? "打开文件失败", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开入口文件失败: {Path}", _source.EntryFilePath);
            Snackbar.Add($"打开失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 打开文件位置
    /// </summary>
    private async Task HandleOpenLocationAsync()
    {
        if (_source == null) return;

        if (IsRemoteAccess)
        {
            Snackbar.Add("远程访问时无法打开本地文件位置", Severity.Warning);
            return;
        }

        try
        {
            var result = _source.IsFolder
                ? await FileExplorerService.OpenFolderInExplorerAsync(_source.FullPath)
                : await FileExplorerService.ShowFileInExplorerAsync(_source.FullPath);

            if (!result.Success)
            {
                Snackbar.Add(result.ErrorMessage ?? "打开文件位置失败", Severity.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "打开文件位置失败: {Path}", _source.FullPath);
            Snackbar.Add($"打开失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 重新识别
    /// </summary>
    private async Task HandleReidentifyAsync()
    {
        if (_source == null) return;

        var initOptions = Config.Identification.ToIdentificationOptions();
        initOptions.AutoAddToDatabase = false;
        initOptions.SkipCache = true; // 重新识别时跳过缓存

        var parameters = new DialogParameters
        {
            { "SourcePath", _source.FullPath },
            { "InitialOptions", initOptions }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<IdentificationOptionsDialog>("识别选项配置", parameters, options);
        var result = await dialog.Result;

        if (result == null || result.Canceled)
            return;

        var identificationOptions = result.Data as Core.Models.Identification.IdentificationOptions;
        if (identificationOptions == null)
        {
            Snackbar.Add("获取识别选项失败", Severity.Error);
            return;
        }

        using var cancellationTokenSource = new CancellationTokenSource();
        var progressReporter = new DialogProgressReporter();

        var loadingParameters = new DialogParameters
        {
            {
                "OnCancel", EventCallback.Factory.Create(this, () =>
                {
                    cancellationTokenSource.Cancel();
                    Snackbar.Add("已请求取消识别", Severity.Info);
                })
            }
        };

        var loadingOptions = new DialogOptions
        {
            NoHeader = true,
            CloseButton = false,
            CloseOnEscapeKey = false,
            Position = DialogPosition.Center
        };

        var loadingDialogRef = await DialogService.ShowAsync<IdentificationLoadingDialog>("", loadingParameters, loadingOptions);
        var dialogInstance = loadingDialogRef.Dialog as IdentificationLoadingDialog;

        progressReporter.OnProgress += (entry) =>
        {
            dialogInstance?.HandleProgress(entry);
        };

        try
        {
            var media = await FilesService.GetMediaByPath(_source.FullPath, identificationOptions, progressReporter, cancellationTokenSource.Token);
            loadingDialogRef.Close();

            if (media != null)
            {
                await ShowMediaInfoDialogAsync(media);
                await LoadSourceAsync();
            }
            else
            {
                Snackbar.Add($"无法识别: {_source.FullPath}", Severity.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            loadingDialogRef.Close();
            Snackbar.Add("识别已取消", Severity.Info);
        }
        catch (Exception ex)
        {
            loadingDialogRef.Close();
            Log.Error(ex, "识别媒体失败: {Path}", _source.FullPath);
            Snackbar.Add($"识别失败: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// 显示媒体信息对话框。对话框内部完成入库流程并反馈 loading/错误状态。
    /// </summary>
    private async Task ShowMediaInfoDialogAsync(Core.Models.Media.MediaBase media)
    {
        var parameters = new DialogParameters
        {
            { nameof(MediaInfoDialog.Media), media },
            {
                nameof(MediaInfoDialog.OnConfirmAsync),
                new Func<Core.Models.Media.MediaBase, Task>(async m => await FilesService.AddMediaToDatabase(m))
            }
        };
        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            Position = DialogPosition.Center
        };

        var dialog = await DialogService.ShowAsync<MediaInfoDialog>("媒体信息", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add($"添加媒体到数据库成功: {media.Title}", Severity.Success);
        }
    }
    

    /// <summary>
    /// 获取类型图标
    /// </summary>
    private string GetCategoryIcon(TopCategory category) => category switch
    {
        TopCategory.Video => Icons.Material.Filled.SmartDisplay,
        TopCategory.Audio => Icons.Material.Filled.Headphones,
        TopCategory.Picture => Icons.Material.Filled.Image,
        TopCategory.Text => Icons.Material.Filled.LibraryBooks,
        TopCategory.Game => Icons.Material.Filled.VideogameAsset,
        _ => Icons.Material.Filled.QuestionMark
    };

    /// <summary>
    /// 获取类型显示名称
    /// </summary>
    private string GetCategoryName(TopCategory category) => category switch
    {
        TopCategory.Video => "视频",
        TopCategory.Audio => "音频",
        TopCategory.Picture => "图片",
        TopCategory.Text => "文本",
        TopCategory.Game => "游戏",
        _ => "未知"
    };

    /// <summary>
    /// 获取类型颜色
    /// </summary>
    private Color GetCategoryColor(TopCategory category) => category switch
    {
        TopCategory.Video => Color.Primary,
        TopCategory.Audio => Color.Secondary,
        TopCategory.Picture => Color.Success,
        TopCategory.Text => Color.Warning,
        TopCategory.Game => Color.Info,
        _ => Color.Default
    };

    /// <summary>
    /// 根据文件扩展名获取文件类型图标
    /// </summary>
    private string GetFileTypeIcon(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            // 视频
            ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm" => Icons.Material.Filled.VideoFile,
            // 音频
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" or ".wma" => Icons.Material.Filled.AudioFile,
            // 图片
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg" => Icons.Material.Filled.Image,
            // 文档
            ".pdf" => Icons.Material.Filled.PictureAsPdf,
            ".doc" or ".docx" => Icons.Material.Filled.Description,
            ".txt" or ".md" => Icons.Material.Filled.Article,
            // 可执行文件
            ".exe" or ".msi" => Icons.Material.Filled.Terminal,
            // 压缩包
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => Icons.Material.Filled.FolderZip,
            // 默认
            _ => Icons.Material.Filled.InsertDriveFile
        };
    }

    /// <summary>
    /// 格式化文件大小
    /// </summary>
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// 获取相对路径（用于入口文件下拉列表显示）
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        if (_source == null || string.IsNullOrEmpty(_source.FullPath))
            return fullPath;

        if (fullPath.StartsWith(_source.FullPath))
        {
            var relative = fullPath.Substring(_source.FullPath.Length).TrimStart(Path.DirectorySeparatorChar);
            return string.IsNullOrEmpty(relative) ? Path.GetFileName(fullPath) : relative;
        }

        return Path.GetFileName(fullPath);
    }

    public void Dispose()
    {
        // 资源释放
    }
}
