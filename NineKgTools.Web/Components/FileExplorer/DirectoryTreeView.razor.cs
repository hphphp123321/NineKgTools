using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Categories;
using Serilog;

namespace NineKgTools.Components.FileExplorer;

// 只读目录树：展示文件夹结构并支持按媒体类别高亮
public partial class DirectoryTreeView : ComponentBase
{
    [Parameter] public string RootPath { get; set; } = "";
    [Parameter] public TopCategory? HighlightCategory { get; set; }
    [Parameter] public EventCallback<string> OnFileSelected { get; set; }

    /// <summary>来自父组件的单向输入；内部通过 _selectedFilePath 镜像，避免在组件内写回 Parameter</summary>
    [Parameter] public string? SelectedFilePath { get; set; }

    /// <summary>最大展示深度，0 = 无限制</summary>
    [Parameter] public int MaxDepth { get; set; } = 3;

    [Parameter] public bool ShowFiles { get; set; } = true;
    [Parameter] public bool ShowHidden { get; set; }

    private List<TreeItemData<TreeNode>> _rootNodes = new();
    private bool _isLoading = true;
    private string? _errorMessage;
    // 上一次成功加载的 RootPath；用它判等避免旧版 "_rootNodes.Count == 0" bug（RootPath 切换时无法重载）
    private string? _lastLoadedRootPath;
    // SelectedFilePath 的内部镜像；父组件通过 OnFileSelected 回调得知变更
    private string? _selectedFilePath;

    protected override async Task OnInitializedAsync()
    {
        await LoadDirectoryTreeAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        // 同步 SelectedFilePath 到内部镜像；父组件更新时覆盖，组件内的临时变化不会被传回
        _selectedFilePath = SelectedFilePath;

        if (!string.IsNullOrEmpty(RootPath) && RootPath != _lastLoadedRootPath)
        {
            await LoadDirectoryTreeAsync();
        }
    }

    private async Task LoadDirectoryTreeAsync()
    {
        if (string.IsNullOrEmpty(RootPath))
        {
            _errorMessage = "没有指定要浏览的路径";
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(RootPath))
                {
                    _rootNodes = LoadChildren(RootPath, 0);
                }
                else if (File.Exists(RootPath))
                {
                    // 如果是文件，直接显示文件信息
                    var fileInfo = new FileInfo(RootPath);
                    var node = new TreeNode
                    {
                        Name = fileInfo.Name,
                        FullPath = RootPath,
                        IsDirectory = false,
                        Extension = fileInfo.Extension.ToLowerInvariant(),
                        Size = fileInfo.Length
                    };
                    _rootNodes = new List<TreeItemData<TreeNode>>
                    {
                        new TreeItemData<TreeNode> { Value = node }
                    };
                }
                else
                {
                    _errorMessage = "路径不存在或已被移动";
                }
            });
            _lastLoadedRootPath = RootPath;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载目录树失败: {Path}", RootPath);
            _errorMessage = "加载目录失败，请检查路径是否可访问。";
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private List<TreeItemData<TreeNode>> LoadChildren(string path, int currentDepth)
    {
        var nodes = new List<TreeItemData<TreeNode>>();

        try
        {
            var dirInfo = new DirectoryInfo(path);

            // 加载子目录
            foreach (var dir in dirInfo.GetDirectories())
            {
                // 跳过隐藏目录
                if (!ShowHidden && (dir.Attributes & FileAttributes.Hidden) != 0)
                    continue;

                var node = new TreeNode
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    IsDirectory = true
                };

                var treeItem = new TreeItemData<TreeNode>
                {
                    Value = node,
                    Expanded = false
                };

                // 如果未达到最大深度，预加载子节点
                if (MaxDepth == 0 || currentDepth < MaxDepth - 1)
                {
                    treeItem.Children = LoadChildren(dir.FullName, currentDepth + 1);
                }
                else
                {
                    // 检查是否有子项
                    try
                    {
                        bool hasChildren = dir.GetDirectories().Length > 0 ||
                                           (ShowFiles && dir.GetFiles().Length > 0);
                        if (hasChildren)
                        {
                            // 添加空子节点列表以显示展开箭头
                            treeItem.Children = new List<TreeItemData<TreeNode>>();
                        }
                    }
                    catch
                    {
                        // 忽略访问错误
                    }
                }

                nodes.Add(treeItem);
            }

            // 加载文件
            if (ShowFiles)
            {
                foreach (var file in dirInfo.GetFiles())
                {
                    // 跳过隐藏文件
                    if (!ShowHidden && (file.Attributes & FileAttributes.Hidden) != 0)
                        continue;

                    var node = new TreeNode
                    {
                        Name = file.Name,
                        FullPath = file.FullName,
                        IsDirectory = false,
                        Extension = file.Extension.ToLowerInvariant(),
                        Size = file.Length
                    };

                    nodes.Add(new TreeItemData<TreeNode> { Value = node });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "无法访问目录: {Path}", path);
        }

        return nodes;
    }

    private async Task SelectFileAsync(TreeNode? node)
    {
        if (node != null && !node.IsDirectory)
        {
            _selectedFilePath = node.FullPath;
            await OnFileSelected.InvokeAsync(node.FullPath);
        }
    }

    private bool IsHighlightedFile(TreeNode? node)
    {
        if (node == null || node.IsDirectory || HighlightCategory == null)
            return false;

        var extensions = TopCategoryExtensions.GetExtensions(HighlightCategory.Value);
        return extensions.Contains(node.Extension);
    }

    private string GetFileIcon(TreeNode? node)
    {
        if (node == null)
            return Icons.Material.Filled.InsertDriveFile;
        if (node.IsDirectory)
            return FileIconHelper.GetFolderIcon();
        return FileIconHelper.GetFileIcon(node.Extension);
    }

    private Color GetIconColor(TreeNode? node)
    {
        if (node == null) return Color.Default;
        if (node.IsDirectory) return FileIconHelper.GetFolderColor();

        // 高亮命中与选中态优先级高于扩展名色
        if (IsHighlightedFile(node)) return Color.Success;
        if (node.FullPath == _selectedFilePath) return Color.Primary;

        return FileIconHelper.GetFileColor(node.Extension);
    }

    // 固定用 Invariant 格式化，避免非英语 locale（如德语）用逗号作小数点
    private static readonly string[] SizeUnits = { "B", "KB", "MB", "GB", "TB" };

    private static string FormatFileSize(long bytes)
    {
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < SizeUnits.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{size:0.##} {SizeUnits[order]}");
    }

    public async Task RefreshAsync()
    {
        _rootNodes.Clear();
        await LoadDirectoryTreeAsync();
    }

    public class TreeNode
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public bool IsDirectory { get; set; }
        public string Extension { get; set; } = "";
        public long Size { get; set; }
    }
}
