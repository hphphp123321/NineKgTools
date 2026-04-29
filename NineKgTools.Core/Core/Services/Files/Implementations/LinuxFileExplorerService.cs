using System.Diagnostics;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Files.Models;
using Serilog;

namespace NineKgTools.Core.Services.Files.Implementations;

/// <summary>
/// Linux 平台文件浏览器服务实现
/// </summary>
public class LinuxFileExplorerService : IFileExplorerService
{
    public bool IsLocalAccessSupported => true;
    public PlatformType CurrentPlatform => PlatformType.Linux;

    public Task<FileOperationResult> ShowFileInExplorerAsync(string filePath)
    {
        return Task.Run(() =>
        {
            try
            {
                if (!File.Exists(filePath))
                    return FileOperationResult.Fail($"文件不存在: {filePath}", FileOperationError.FileNotFound);

                // Linux: xdg-open 打开父目录（大多数 Linux 文件管理器不支持直接选中文件）
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrEmpty(directory))
                    return FileOperationResult.Fail("无法获取文件所在目录", FileOperationError.InvalidPath);

                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{directory}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Log.Information("在文件管理器中显示文件（打开父目录）: {FilePath}", filePath);
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "在文件管理器中显示文件失败: {FilePath}", filePath);
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

                // Linux: xdg-open "path"
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Log.Information("在文件管理器中打开文件夹: {FolderPath}", folderPath);
                return FileOperationResult.Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "在文件管理器中打开文件夹失败: {FolderPath}", folderPath);
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

                // Linux: xdg-open "path"
                var psi = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
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
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    CreateNoWindow = true
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
