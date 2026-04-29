using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Files.Models;
using Serilog;

namespace NineKgTools.Core.Services.Files.Implementations;

/// <summary>
/// 远程访问时的文件浏览器服务实现（Web 环境）
/// 所有操作都不受支持，返回错误结果
/// </summary>
public class RemoteFileExplorerService : IFileExplorerService
{
    public bool IsLocalAccessSupported => false;
    public PlatformType CurrentPlatform => PlatformType.Remote;

    private const string NotSupportedMessage = "远程访问时不支持本地文件系统操作，请在本地访问或使用桌面客户端";

    public Task<FileOperationResult> ShowFileInExplorerAsync(string filePath)
    {
        Log.Warning("尝试在远程环境中显示文件: {FilePath}", filePath);
        return Task.FromResult(FileOperationResult.Fail(NotSupportedMessage, FileOperationError.NotSupported));
    }

    public Task<FileOperationResult> OpenFolderInExplorerAsync(string folderPath)
    {
        Log.Warning("尝试在远程环境中打开文件夹: {FolderPath}", folderPath);
        return Task.FromResult(FileOperationResult.Fail(NotSupportedMessage, FileOperationError.NotSupported));
    }

    public Task<FileOperationResult> OpenFileWithDefaultAppAsync(string filePath)
    {
        Log.Warning("尝试在远程环境中打开文件: {FilePath}", filePath);
        return Task.FromResult(FileOperationResult.Fail(NotSupportedMessage, FileOperationError.NotSupported));
    }

    public Task<FileOperationResult> RunExecutableAsync(string executablePath, string? arguments = null)
    {
        Log.Warning("尝试在远程环境中运行程序: {ExecutablePath}", executablePath);
        return Task.FromResult(FileOperationResult.Fail(NotSupportedMessage, FileOperationError.NotSupported));
    }
}
