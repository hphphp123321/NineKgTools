using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Components.Common;
using NineKgTools.Components.Medias;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Media;
using NineKgTools.Core.Services.Media.QueryParameters;
using NineKgTools.Core.Services.Source;
using NineKgTools.Utils;
using Serilog;

namespace NineKgTools.Pages.Sources;

/// <summary>
/// 文件排序字段
/// </summary>
public enum FileSortField
{
    Name,       // 名称
    Date,       // 修改日期
    Type        // 类型（扩展名）
}

public partial class SourcesPage : ComponentBase
{
    [Inject] private Config Config { get; set; }
    [Inject] private ISnackbar Snackbar { get; set; }
    [Inject] private FilesService FilesService { get; set; }
    [Inject] private MonitorService MonitorService { get; set; }
    [Inject] private MediaService MediaService { get; set; }
    [Inject] private SourceService SourceService { get; set; }
    [Inject] private IDialogService DialogService { get; set; }
    [Inject] private NavigationManager NavigationManager { get; set; }

    private SourceConfig _sourceConfig = new();

    // 文件浏览相关
    private DirectoryInfo? _currentPathInfo;
    private List<DirectoryInfo> _directories = new();
    private List<FileInfo> _files = new();
    private List<BreadcrumbItem> _breadcrumbs = new();
    private List<DriveInfo> _drives = new();

    // 当前目录中已识别（数据库存在 MediaSource 记录）的路径 → MediaSource.Id 映射
    // RefreshCurrentDirectory 末尾 fire-and-forget 异步加载，加载完后通过 InvokeAsync(StateHasChanged) 重渲。
    // 仅用于"查看详情"按钮的条件渲染：有映射则显示按钮直跳 SourceDetailPage，无映射则不显示。
    private Dictionary<string, int> _identifiedSourceIds = new();

    // 排序相关
    private FileSortField _sortField = FileSortField.Name;
    private bool _sortAscending = true;

    // 最近识别的媒体列表
    private List<MediaBase> _recentIdentifiedMedia = new();

    // 目录读取错误信息（内联显示在文件列表区域）
    private string? _directoryError;

    // 加载状态
    private bool _isLoadingRecent = true;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _sourceConfig = Config.Source.Copy();

        // 获取所有驱动器（安全处理驱动器未就绪的情况）
        try
        {
            _drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "获取驱动器列表失败");
            _drives = new List<DriveInfo>();
        }

        // 初始化文件浏览器 — 优先使用用户主目录，回退到第一个可用驱动器
        var startPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(startPath) || !Directory.Exists(startPath))
        {
            startPath = _drives.FirstOrDefault()?.RootDirectory.FullName
                        ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        if (!string.IsNullOrEmpty(startPath))
            NavigateToPath(startPath);

        // 加载最近识别的媒体
        LoadRecentIdentifiedMedia();
    }

    // 加载最近识别的媒体
    private void LoadRecentIdentifiedMedia()
    {
        _isLoadingRecent = true;
        try
        {
            var parameters = new MediaQueryParameters
            {
                PageNumber = 1,
                PageSize = 5,
                SortOption = MediaSortOption.StoreDateDesc
            };

            var mediaList = MediaService.GetPagedMediaList(parameters);
            _recentIdentifiedMedia = mediaList.ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载最近识别的媒体失败");
            Snackbar.Add("加载最近识别的媒体失败", Severity.Error);
        }
        finally
        {
            _isLoadingRecent = false;
        }
    }

    /// <summary>
    /// 手动添加媒体 —— 跳过识别流程，为指定路径创建空 MediaBase 并直接入库。
    /// 若路径已有关联媒体则导航到现有详情；若对应 MediaSource 已存在于数据库（但未入库）则复用该源。
    /// 场景：识别源搜不到该作品（个人录制、冷门资源、还未扫描入库的源）。
    /// </summary>
    private Task HandleManualAddFromPathAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.CompletedTask;
        return ManualAddMediaHelper.OpenByPathAsync(
            path, DialogService, SourceService, MediaService, Snackbar, NavigationManager);
    }

    // 尝试识别媒体（立即返回结果，并且默认不添加到数据库）
    private async Task HandleIdentifyMedia(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var initOptions = Config.Identification.ToIdentificationOptions();
        initOptions.AutoAddToDatabase = false;

        var parameters = new DialogParameters
        {
            { "SourcePath", path },
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

        using var cancellationTokenSource = new System.Threading.CancellationTokenSource();
        var progressReporter = new DialogProgressReporter();

        var loadingParameters = new DialogParameters
        {
            { "OnCancel", EventCallback.Factory.Create(this, () =>
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
            var media = await FilesService.GetMediaByPath(path, identificationOptions, progressReporter, cancellationTokenSource.Token);
            loadingDialogRef.Close();

            if (media != null)
            {
                await ShowMediaInfoDialog(media);
            }
            else
            {
                Snackbar.Add($"无法识别: {Path.GetFileName(path)}", Severity.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            loadingDialogRef.Close();
            Log.Information("识别已被用户取消: {Path}", path);
            Snackbar.Add("识别已取消", Severity.Info);
        }
        catch (Exception ex)
        {
            loadingDialogRef.Close();
            Log.Error(ex, "识别媒体失败: {Path}", path);
            Snackbar.Add($"识别失败: {ex.Message}", Severity.Error);
        }
        finally
        {
            LoadRecentIdentifiedMedia();
            StateHasChanged();
        }
    }

    // 加入识别队列（提交任务后立即返回）
    private async Task HandleAddToIdentificationQueue(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        var parameters = new DialogParameters
        {
            { "SourcePath", path },
            { "InitialOptions", null }
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

        try
        {
            var taskId = await FilesService.IdentifySingleMedia(path, identificationOptions);
            var fileName = Path.GetFileName(path);
            Snackbar.Add($"已将 {fileName} 加入识别队列，任务ID: {taskId}", Severity.Success);
            Log.Information("媒体识别任务已提交: {Path}, TaskId: {TaskId}", path, taskId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "提交识别任务失败: {Path}", path);
            Snackbar.Add($"提交任务失败: {ex.Message}", Severity.Error);
        }
    }

    // 显示媒体信息弹窗
    private async Task ShowMediaInfoDialog(MediaBase media)
    {
        var parameters = new DialogParameters
        {
            { nameof(MediaInfoDialog.Media), media },
            {
                nameof(MediaInfoDialog.OnConfirmAsync),
                new Func<MediaBase, Task>(async m => await FilesService.AddMediaToDatabase(m))
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

    private async Task _deleteSourceFolder(string folder)
    {
        _sourceConfig.WatchFolders.Remove(folder);
        try
        {
            await MonitorService.StopMonitoring(folder);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "停止监控文件夹失败: {Folder}", folder);
        }

        Config.Source.WatchFolders = _sourceConfig.WatchFolders.ToList();
        await Config.SaveConfig();

        Snackbar.Add("删除成功", Severity.Success);
    }

    private async Task _addSourceFolder(string folder)
    {
        if (_sourceConfig.WatchFolders.Contains(folder))
        {
            Snackbar.Add("目录已存在", Severity.Warning);
            return;
        }

        _sourceConfig.WatchFolders.Add(folder);

        try
        {
            var taskId = await FilesService.IdentifyBatchMedia(folder);
            Snackbar.Add($"目录 {Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar))} 已添加至监视列表，批量识别任务已提交", Severity.Success);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "批量识别任务提交失败: {Folder}", folder);
            Snackbar.Add($"目录已添加，但批量识别任务提交失败: {ex.Message}", Severity.Warning);
        }

        Config.Source.WatchFolders = _sourceConfig.WatchFolders.ToList();
        await Config.SaveConfig();
    }

    private async Task _saveConfig()
    {
        Log.Debug("保存媒体源配置项");

        Config.Source.WatchFolders = _sourceConfig.WatchFolders.ToList();

        await Config.SaveConfig();
        Snackbar.Add("保存媒体源成功", Severity.Success);
    }

    // 导航到指定路径
    private void NavigateToPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            _currentPathInfo = new DirectoryInfo(path);
            _directoryError = null;

            RefreshCurrentDirectory();
            UpdateBreadcrumbs();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "无法访问路径: {Path}", path);
            Snackbar.Add($"无法访问路径: {ex.Message}", Severity.Error);
        }
    }

    // 导航到父级目录
    private void NavigateToParent()
    {
        if (_currentPathInfo?.Parent != null)
        {
            NavigateToPath(_currentPathInfo.Parent.FullName);
        }
    }

    // 导航到指定驱动器
    private void NavigateToDrive(DriveInfo drive)
    {
        // 驱动器可能在页面加载后断开
        try
        {
            if (!drive.IsReady)
            {
                Snackbar.Add($"驱动器 {drive.Name} 当前不可用", Severity.Warning);
                return;
            }
            NavigateToPath(drive.RootDirectory.FullName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "访问驱动器失败: {Drive}", drive.Name);
            Snackbar.Add($"访问驱动器 {drive.Name} 失败", Severity.Error);
        }
    }

    // 刷新当前目录
    private void RefreshCurrentDirectory()
    {
        _directoryError = null;

        try
        {
            if (_currentPathInfo == null || !_currentPathInfo.Exists)
            {
                _directoryError = "目录不存在或已被删除";
                _directories = new List<DirectoryInfo>();
                _files = new List<FileInfo>();
                return;
            }

            // 安全获取子目录 — 跳过无权访问的条目
            var directories = new List<DirectoryInfo>();
            try
            {
                directories.AddRange(_currentPathInfo.GetDirectories());
            }
            catch (UnauthorizedAccessException)
            {
                _directoryError = "没有权限读取此目录的子文件夹";
            }

            var files = new List<FileInfo>();
            try
            {
                files.AddRange(_currentPathInfo.GetFiles());
            }
            catch (UnauthorizedAccessException)
            {
                _directoryError = _directoryError != null
                    ? "没有权限读取此目录"
                    : "没有权限读取此目录的文件";
            }

            _directories = SortDirectories(directories).ToList();
            _files = SortFiles(files).ToList();

            // 部分成功：有数据但也有权限问题 — 只在 Snackbar 提示，不阻塞列表
            if (_directoryError != null && (_directories.Count > 0 || _files.Count > 0))
            {
                Snackbar.Add(_directoryError, Severity.Warning);
                _directoryError = null; // 清除内联错误，因为有部分数据可显示
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "读取目录失败: {Path}", _currentPathInfo?.FullName);
            _directoryError = $"读取目录失败: {ex.Message}";
            _directories = new List<DirectoryInfo>();
            _files = new List<FileInfo>();
        }

        // 切换/刷新目录时清空旧缓存，避免上一目录的按钮在新目录里错误显示
        _identifiedSourceIds = new Dictionary<string, int>();
        _ = LoadIdentifiedSourceIdsAsync();
    }

    /// <summary>
    /// 异步批量查询当前目录中已被数据库识别的路径，更新 _identifiedSourceIds 缓存并触发重渲。
    /// fire-and-forget 调用：失败仅 Log.Warning，不阻塞主流程；查询前快照本次目录路径集，
    /// 完成后比对快照防止用户已切走目录后写入过期数据（race condition）。
    /// </summary>
    private async Task LoadIdentifiedSourceIdsAsync()
    {
        try
        {
            var allPaths = _directories.Select(d => d.FullName)
                .Concat(_files.Select(f => f.FullName))
                .ToList();

            if (allPaths.Count == 0)
                return;

            // 快照当前目录的第一个路径作为 race condition 检测信号
            var snapshotKey = _currentPathInfo?.FullName;
            var ids = await SourceService.GetIdsForPathsAsync(allPaths);

            // 用户在查询期间切换了目录 → 丢弃过期结果
            if (_currentPathInfo?.FullName != snapshotKey)
                return;

            _identifiedSourceIds = ids;
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载已识别媒体源 ID 映射失败");
        }
    }

    /// <summary>
    /// 给指定路径返回 SourceDetailPage 的链接；未识别返回 null（调用方据此决定是否渲染按钮）
    /// </summary>
    private string? GetSourceDetailLink(string fullPath)
        => _identifiedSourceIds.TryGetValue(fullPath, out var id) ? $"/source/{id}" : null;

    // 对文件夹进行排序
    private IEnumerable<DirectoryInfo> SortDirectories(IEnumerable<DirectoryInfo> directories)
    {
        return _sortField switch
        {
            FileSortField.Name => _sortAscending
                ? directories.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                : directories.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase),
            FileSortField.Date => _sortAscending
                ? directories.OrderBy(d => d.LastWriteTime)
                : directories.OrderByDescending(d => d.LastWriteTime),
            FileSortField.Type => _sortAscending
                ? directories.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                : directories.OrderByDescending(d => d.Name, StringComparer.OrdinalIgnoreCase),
            _ => directories.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
        };
    }

    // 对文件进行排序
    private IEnumerable<FileInfo> SortFiles(IEnumerable<FileInfo> files)
    {
        return _sortField switch
        {
            FileSortField.Name => _sortAscending
                ? files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
            FileSortField.Date => _sortAscending
                ? files.OrderBy(f => f.LastWriteTime)
                : files.OrderByDescending(f => f.LastWriteTime),
            FileSortField.Type => _sortAscending
                ? files.OrderBy(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                : files.OrderByDescending(f => f.Extension, StringComparer.OrdinalIgnoreCase).ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
        };
    }

    // 设置排序字段
    private void SetSortField(FileSortField field)
    {
        if (_sortField == field)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortField = field;
            _sortAscending = true;
        }
        RefreshCurrentDirectory();
    }

    // 获取排序图标
    private string GetSortIcon(FileSortField field)
    {
        if (_sortField != field)
            return Icons.Material.Filled.UnfoldMore;
        return _sortAscending ? Icons.Material.Filled.ArrowUpward : Icons.Material.Filled.ArrowDownward;
    }

    // 更新面包屑导航
    private void UpdateBreadcrumbs()
    {
        _breadcrumbs.Clear();

        if (_currentPathInfo == null) return;

        var pathParts = new List<DirectoryInfo>();
        var current = _currentPathInfo;

        while (current != null)
        {
            pathParts.Insert(0, current);
            current = current.Parent;
        }

        foreach (var part in pathParts)
        {
            _breadcrumbs.Add(new BreadcrumbItem(
                part.Name == "" ? part.FullName : part.Name,
                part.FullName,
                false));
        }
    }

    // 根据文件名获取文件图标
    private string GetFileIcon(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext))
            return Icons.Material.Filled.InsertDriveFile;

        return ext.ToLowerInvariant() switch
        {
            ".pdf" => Icons.Material.Filled.PictureAsPdf,
            ".doc" or ".docx" => Icons.Material.Filled.Description,
            ".xls" or ".xlsx" => Icons.Material.Filled.TableChart,
            ".zip" or ".rar" or ".7z" => Icons.Material.Filled.Archive,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => Icons.Material.Filled.Image,
            ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => Icons.Material.Filled.MusicNote,
            ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => Icons.Material.Filled.Movie,
            ".exe" or ".msi" => Icons.Material.Filled.Terminal,
            ".txt" or ".log" or ".md" => Icons.Material.Filled.TextSnippet,
            ".json" or ".xml" or ".yaml" or ".yml" => Icons.Material.Filled.DataObject,
            _ => Icons.Material.Filled.InsertDriveFile
        };
    }

    // 将文件夹添加到监视列表
    private async Task AddToWatchList(string folderPath)
    {
        if (_sourceConfig.WatchFolders.Contains(folderPath))
        {
            Snackbar.Add("此文件夹已在监视列表中", Severity.Warning);
            return;
        }

        await _addSourceFolder(folderPath);
    }
}
