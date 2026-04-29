using NineKgTools.Core.Services.Files.Models;

namespace NineKgTools.Core.Services.Files.Interfaces;

/// <summary>
/// 文件浏览器操作服务接口
/// 用于在系统资源管理器中打开/显示文件和文件夹
/// </summary>
public interface IFileExplorerService
{
    /// <summary>
    /// 是否支持本地文件系统操作
    /// 本地访问时为 true，远程访问时为 false
    /// </summary>
    bool IsLocalAccessSupported { get; }

    /// <summary>
    /// 获取当前平台类型
    /// </summary>
    PlatformType CurrentPlatform { get; }

    /// <summary>
    /// 在资源管理器中显示文件（选中文件）
    /// Windows: explorer /select, "path"
    /// macOS: open -R "path"
    /// Linux: xdg-open (打开父目录)
    /// </summary>
    /// <param name="filePath">文件的完整路径</param>
    /// <returns>操作结果</returns>
    Task<FileOperationResult> ShowFileInExplorerAsync(string filePath);

    /// <summary>
    /// 在资源管理器中打开文件夹
    /// </summary>
    /// <param name="folderPath">文件夹的完整路径</param>
    /// <returns>操作结果</returns>
    Task<FileOperationResult> OpenFolderInExplorerAsync(string folderPath);

    /// <summary>
    /// 使用系统默认程序打开文件
    /// </summary>
    /// <param name="filePath">文件的完整路径</param>
    /// <returns>操作结果</returns>
    Task<FileOperationResult> OpenFileWithDefaultAppAsync(string filePath);

    /// <summary>
    /// 运行可执行文件（游戏、程序等）
    /// </summary>
    /// <param name="executablePath">可执行文件的完整路径</param>
    /// <param name="arguments">启动参数（可选）</param>
    /// <returns>操作结果</returns>
    Task<FileOperationResult> RunExecutableAsync(string executablePath, string? arguments = null);
}
