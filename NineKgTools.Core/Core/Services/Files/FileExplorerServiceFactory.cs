using System.Runtime.InteropServices;
using NineKgTools.Core.Services.Files.Implementations;
using NineKgTools.Core.Services.Files.Interfaces;
using NineKgTools.Core.Services.Files.Models;
using Serilog;

namespace NineKgTools.Core.Services.Files;

/// <summary>
/// 文件浏览器服务工厂
/// 负责检测当前平台并创建对应的服务实现
/// </summary>
public static class FileExplorerServiceFactory
{
    /// <summary>
    /// 获取当前运行平台类型
    /// </summary>
    public static PlatformType GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return PlatformType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return PlatformType.MacOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return PlatformType.Linux;

        return PlatformType.Unknown;
    }

    /// <summary>
    /// 根据当前平台和访问模式创建文件浏览器服务
    /// </summary>
    /// <param name="isLocalAccess">是否为本地访问（非远程访问）</param>
    /// <returns>对应平台的文件浏览器服务实例</returns>
    public static IFileExplorerService Create(bool isLocalAccess)
    {
        // 如果是远程访问，返回远程实现
        if (!isLocalAccess)
        {
            Log.Debug("检测到远程访问模式，使用 RemoteFileExplorerService");
            return new RemoteFileExplorerService();
        }

        var platform = GetCurrentPlatform();
        Log.Debug("检测到本地访问，平台: {Platform}", platform);

        return platform switch
        {
            PlatformType.Windows => new WindowsFileExplorerService(),
            PlatformType.MacOS => new MacFileExplorerService(),
            PlatformType.Linux => new LinuxFileExplorerService(),
            _ => new RemoteFileExplorerService() // 未知平台使用远程实现（不支持本地操作）
        };
    }
}
