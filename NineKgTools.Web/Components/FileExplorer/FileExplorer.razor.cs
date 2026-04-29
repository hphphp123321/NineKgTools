using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Serilog;

namespace NineKgTools.Components.FileExplorer;

public static class FileSelectMode
{
    public const string File = "file";
    public const string Folder = "folder";
}

public enum FileExplorerSortField
{
    Name,
    Date,
    Type
}

public partial class FileExplorer : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    /// <summary>文件选择模式：file = 选文件（默认）；folder = 选文件夹</summary>
    [Parameter] public string SelectFolderMode { get; set; } = FileSelectMode.File;
    [Parameter] public string? StartingDirectory { get; set; }
    [Parameter] public EventCallback<string> OnPathSelected { get; set; }

    [Parameter] public FilenameFilter[]? Filters { get; set; } =
    {
        FilenameFilter.Any()
    };

    [Parameter] public bool AllowEdit { get; set; }

    // 当前过滤器索引。setter 原本同步调用 ListFiles()，现在改用
    // OnFilterChanged 异步处理以避免阻塞 hub 线程
    private int _filterIndex;
    private string? _selectedFilterName;

    private FilenameFilter? Filter =>
        Filters != null && _filterIndex >= 0 && _filterIndex < Filters.Length
            ? Filters[_filterIndex]
            : null;

    private async Task OnFilterChanged(string? filterName)
    {
        _selectedFilterName = filterName;
        if (Filters != null && filterName != null)
        {
            _filterIndex = Array.IndexOf(Filters, Filters.FirstOrDefault(f => f.Name == filterName));
        }

        _isLoading = true;
        StateHasChanged();
        try
        {
            await Task.Run(ListFiles);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private string _currentDirectory = null!;
    private string? _currentSelectedName;

    private string FullPath =>
        _currentSelectedName == null ? _currentDirectory : Path.Combine(_currentDirectory, _currentSelectedName);

    // 是否已在根目录；用于禁用"上一级"按钮
    private bool _isAtRoot;

    // 排序相关
    private FileExplorerSortField _sortField = FileExplorerSortField.Name;
    private bool _sortAscending = true;

    // 当前是否正在执行 I/O；用于禁用导航按钮 + 显示 spinner
    private bool _isLoading;

    protected override async Task OnInitializedAsync()
    {
        _history.Clear();
        RefreshSystemDirectories();
        await SetDirectoryAsync(StartingDirectory ?? Directory.GetCurrentDirectory());
    }

    // 切换目录：所有文件 I/O 通过 Task.Run 卸载到线程池，避免阻塞 SignalR hub 线程。
    // 否则慢盘、网络盘、权限探测等场景会冻结整个 Blazor Server circuit
    private async Task SetDirectoryAsync(string path, bool clearHistory = true)
    {
        if (clearHistory)
            PopHistory();

        _currentDirectory = Path.GetFullPath(path);
        _isAtRoot = Directory.GetParent(_currentDirectory) == null;

        if (clearHistory)
            PushHistory(path);

        _nameFilter = null; // Clear filter when changing directories
        _currentSelectedName = null; // Deselect file when changing directories

        _isLoading = true;
        StateHasChanged();
        try
        {
            await Task.Run(() =>
            {
                ListFolders();
                ListFiles();
            });
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SelectFile(string? filename)
    {
        _currentSelectedName = filename;
        OnPathSelected.InvokeAsync(FullPath);
    }
    
    private void SelectFolder(string? folder)
    {
        _currentSelectedName = folder;
        OnPathSelected.InvokeAsync(FullPath);
    }

    private string? _nameFilter;

    private bool MatchesNameFilter(string filename)
    {
        if (string.IsNullOrEmpty(_nameFilter))
        {
            return true;
        }

        return filename.IndexOf(_nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private List<string> _subDirs = [];
    // 权限拒绝 / 路径无效等 I/O 失败的用户可见提示
    private string? _directoryAccessError;

    private void ListFolders()
    {
        List<string> paths = [];
        try
        {
            paths.AddRange(Directory.EnumerateDirectories(_currentDirectory));
            _directoryAccessError = null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "访问被拒绝 Directory={Dir}", _currentDirectory);
            _directoryAccessError = "无权访问此目录。";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "枚举子目录失败 Directory={Dir}", _currentDirectory);
            _directoryAccessError = "无法读取此目录。";
        }

        _subDirs = SortFolders(paths).ToList();
    }

    private List<string?> _files = new();

    private void ListFiles()
    {
        List<string?> paths = [];
        try
        {
            paths.AddRange(Directory.EnumerateFiles(_currentDirectory)
                .Select(Path.GetFileName)
                .Where(filename => Filter == null || Filter.Matches(filename)));
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warning(ex, "访问被拒绝 Directory={Dir}", _currentDirectory);
            _directoryAccessError ??= "无权访问此目录。";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "枚举文件失败 Directory={Dir}", _currentDirectory);
            _directoryAccessError ??= "无法读取此目录。";
        }

        _files = SortFileNames(paths).ToList();
    }

    // 对文件夹进行排序
    private IEnumerable<string> SortFolders(IEnumerable<string> folders)
    {
        return _sortField switch
        {
            FileExplorerSortField.Name => _sortAscending
                ? folders.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                : folders.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
            FileExplorerSortField.Date => _sortAscending
                ? folders.OrderBy(f => Directory.GetLastWriteTime(f))
                : folders.OrderByDescending(f => Directory.GetLastWriteTime(f)),
            FileExplorerSortField.Type => _sortAscending
                ? folders.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                : folders.OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
            _ => folders.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        };
    }

    // 对文件名进行排序
    private IEnumerable<string?> SortFileNames(IEnumerable<string?> files)
    {
        return _sortField switch
        {
            FileExplorerSortField.Name => _sortAscending
                ? files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                : files.OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase),
            FileExplorerSortField.Date => _sortAscending
                ? files.OrderBy(f => f != null ? System.IO.File.GetLastWriteTime(Path.Combine(_currentDirectory, f)) : DateTime.MinValue)
                : files.OrderByDescending(f => f != null ? System.IO.File.GetLastWriteTime(Path.Combine(_currentDirectory, f)) : DateTime.MinValue),
            FileExplorerSortField.Type => _sortAscending
                ? files.OrderBy(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase).ThenBy(f => f, StringComparer.OrdinalIgnoreCase)
                : files.OrderByDescending(f => Path.GetExtension(f), StringComparer.OrdinalIgnoreCase).ThenByDescending(f => f, StringComparer.OrdinalIgnoreCase),
            _ => files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        };
    }

    // 设置排序字段。排序需要读取文件元数据（例如按日期要 GetLastWriteTime），同样包 Task.Run
    private async Task SetSortField(FileExplorerSortField field)
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

        _isLoading = true;
        StateHasChanged();
        try
        {
            await Task.Run(() =>
            {
                ListFolders();
                ListFiles();
            });
        }
        finally
        {
            _isLoading = false;
        }
    }

    // 获取排序图标
    private string GetSortIcon(FileExplorerSortField field)
    {
        if (_sortField != field)
            return Icons.Material.Filled.UnfoldMore;
        return _sortAscending ? Icons.Material.Filled.ArrowUpward : Icons.Material.Filled.ArrowDownward;
    }

    // Key = 显示名 / Value = 路径；Windows 是驱动器，Linux/macOS 是 / 下的常用目录
    private readonly Dictionary<string, string> _systemFolders = new();

    // Linux/macOS 下跳过的系统伪目录，这些对普通用户没有浏览价值且可能触发访问拒绝
    private static readonly HashSet<string> LinuxSystemDirs = new(StringComparer.Ordinal)
    {
        "proc", "sys", "dev", "run", "boot", "lost+found", "snap"
    };

    private void RefreshSystemDirectories()
    {
        _systemFolders.Clear();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;
                        var label = string.IsNullOrEmpty(drive.VolumeLabel) ? "本地磁盘" : drive.VolumeLabel;
                        var name = $"{drive.Name.TrimEnd('\\')} ({label})";
                        _systemFolders.Add(name, drive.RootDirectory.FullName);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "枚举驱动器失败 Drive={Drive}", drive.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "GetDrives 调用失败");
            }
        }
        else
        {
            try
            {
                foreach (var dir in Directory.GetDirectories("/"))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name) || LinuxSystemDirs.Contains(name))
                        continue;
                    _systemFolders.Add(name, dir);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "枚举根目录失败");
            }
        }
    }

    private readonly List<string> _history = new();
    private int _historyIndex;
    private const int MaxHistory = 100;

    private void PushHistory(string path)
    {
        _history.Add(path);
        while (_history.Count > MaxHistory)
        {
            _history.RemoveAt(0); // remove oldest
        }

        _historyIndex = _history.Count - 1;
    }

    private void PopHistory()
    {
        var index = _historyIndex + 1;
        if (index < _history.Count)
            _history.RemoveRange(index, _history.Count - index);
    }

    private async Task Back()
    {
        if (_historyIndex > 0)
        {
            _historyIndex--;
        }

        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            await SetDirectoryAsync(_history[_historyIndex], clearHistory: false);
        }
    }

    private async Task Forward()
    {
        if (_historyIndex < (_history.Count - 1))
        {
            _historyIndex++;
        }

        if (_historyIndex >= 0 && _historyIndex < _history.Count)
        {
            await SetDirectoryAsync(_history[_historyIndex], clearHistory: false);
        }
    }

    private async Task Parent()
    {
        var parent = Directory.GetParent(_currentDirectory)?.FullName;
        if (parent != null)
        {
            await SetDirectoryAsync(parent);
        }
    }

    private async Task Refresh()
    {
        RefreshSystemDirectories();
        _isLoading = true;
        StateHasChanged();
        try
        {
            await Task.Run(() =>
            {
                ListFolders();
                ListFiles();
            });
        }
        finally
        {
            _isLoading = false;
        }
    }

    // 实例字段而非 static —— Blazor Server 下 static 字段跨用户共享会泄漏本地目录历史（隐私/安全事故）。
    // 当前实现仅在单次 Dialog 生命期内保留，未来如需跨会话持久化应接入 ProtectedLocalStorage 或用户表。
    private readonly List<string> _recent = new();
    private const int MaxRecent = 4;

    // ==================== 交互事件处理 ====================

    private async Task OnPathKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await SetDirectoryAsync(_currentDirectory);
        }
    }

    // 列表项选择回调由 OnClick 直接处理（SelectFile/SelectFolder/SetDirectoryAsync），
    // MudList 的 SelectedValueChanged 无需额外逻辑
    private static void OnListItemSelected(object? value)
    {
    }

    // ==================== Dialog 生命周期 ====================

    private void Cancel() => MudDialog.Cancel();

    private void Confirm()
    {
        if (string.IsNullOrEmpty(FullPath)) return;
        if (!_recent.Contains(_currentDirectory))
            _recent.Add(_currentDirectory);
        while (_recent.Count > MaxRecent)
        {
            _recent.RemoveAt(0); // remove oldest
        }

        // 返回完整路径而非仅文件名：文件模式下 _files 列表只存文件名（见 ListFiles 里 Path.GetFileName），
        // 文件夹模式下 _subDirs 存的是完整路径；为统一调用方契约，这里始终返回 FullPath。
        MudDialog.Close(DialogResult.Ok(FullPath));
    }
}