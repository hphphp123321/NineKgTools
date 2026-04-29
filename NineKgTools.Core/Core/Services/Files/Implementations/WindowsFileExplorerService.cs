using System.Diagnostics;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Files.Models;
using Serilog;

namespace NineKgTools.Core.Services.Files.Implementations;

/// <summary>
/// Windows 平台文件浏览器服务实现
/// </summary>
public class WindowsFileExplorerService : IFileExplorerService
{
    public bool IsLocalAccessSupported => true;
    public PlatformType CurrentPlatform => PlatformType.Windows;

    public Task<FileOperationResult> ShowFileInExplorerAsync(string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                    return FileOperationResult.Fail($"文件不存在: {filePath}", FileOperationError.FileNotFound);

                // Windows: explorer /select, "path"
                Process.Start("explorer.exe", $"/select, \"{filePath}\"");
                Log.Information("在资源管理器中显示文件: {FilePath}", filePath);
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "在资源管理器中显示文件失败: {FilePath}", filePath);
                return FileOperationResult.Fail(ex.Message, FileOperationError.ProcessStartFailed);
            }
        });
    }

    public Task<FileOperationResult> OpenFolderInExplorerAsync(string folderPath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(folderPath))
                    return FileOperationResult.Fail($"文件夹不存在: {folderPath}", FileOperationError.FileNotFound);

                // Windows: explorer "path"
                Process.Start("explorer.exe", $"\"{folderPath}\"");
                Log.Information("在资源管理器中打开文件夹: {FolderPath}", folderPath);
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "在资源管理器中打开文件夹失败: {FolderPath}", folderPath);
                return FileOperationResult.Fail(ex.Message, FileOperationError.ProcessStartFailed);
            }
        });
    }

    public Task<FileOperationResult> OpenFileWithDefaultAppAsync(string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                    return FileOperationResult.Fail($"文件不存在: {filePath}", FileOperationError.FileNotFound);

                // Windows: 使用 ProcessStartInfo 打开默认程序
                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                Log.Information("使用默认程序打开文件: {FilePath}", filePath);
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "使用默认程序打开文件失败: {FilePath}", filePath);
                return FileOperationResult.Fail(ex.Message, FileOperationError.ProcessStartFailed);
            }
        });
    }

    public Task<FileOperationResult> RunExecutableAsync(string executablePath, string? arguments = null)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(executablePath))
                    return FileOperationResult.Fail($"可执行文件不存在: {executablePath}", FileOperationError.FileNotFound);

                var psi = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)
                };
                Process.Start(psi);
                Log.Information("运行程序: {ExecutablePath} {Arguments}", executablePath, arguments ?? "");
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "运行程序失败: {ExecutablePath}", executablePath);
                return FileOperationResult.Fail(ex.Message, FileOperationError.ProcessStartFailed);
            }
        });
    }
}
