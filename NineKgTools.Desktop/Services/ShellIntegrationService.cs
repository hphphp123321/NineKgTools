using System.Runtime.InteropServices;
#pragma warning disable CA1416 // 平台特定 API 警告——本类整体仅 Windows 调用
using Microsoft.Win32;
#pragma warning restore CA1416
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// Windows 资源管理器右键集成（HKEY_CURRENT_USER 注册表方式，无需 UAC 提权）。
/// 在用户右键 *.* 文件 / 文件夹时显示「用 NineKgTools 识别」选项。
/// 非 Windows 平台所有方法 no-op + 返回 false。
/// </summary>
public class ShellIntegrationService
{
    private const string VerbKeyPath_File = @"Software\Classes\*\shell\NineKgToolsIdentify";
    private const string VerbKeyPath_Folder = @"Software\Classes\Directory\shell\NineKgToolsIdentify";
    private const string VerbDisplayName = "用 NineKgTools 识别";

    public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>
    /// 注册右键 verb（HKCU）。失败返回 false（不抛异常）。
    /// </summary>
    public bool Register()
    {
        if (!IsSupported)
        {
            Log.Information("ShellIntegrationService.Register: 非 Windows 平台，跳过");
            return false;
        }

        try
        {
            var exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Warning("ShellIntegrationService.Register: 无法解析当前 exe 路径");
                return false;
            }

            // 文件 verb
            WriteVerb(VerbKeyPath_File, exePath);
            // 文件夹 verb
            WriteVerb(VerbKeyPath_Folder, exePath);

            Log.Information("Shell verb 已注册到 HKCU：{Exe}", exePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ShellIntegrationService.Register 失败");
            return false;
        }
    }

    /// <summary>
    /// 卸载右键 verb（HKCU）。失败返回 false。
    /// </summary>
    public bool Unregister()
    {
        if (!IsSupported) return false;
        try
        {
            DeleteVerb(VerbKeyPath_File);
            DeleteVerb(VerbKeyPath_Folder);
            Log.Information("Shell verb 已从 HKCU 卸载");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ShellIntegrationService.Unregister 失败");
            return false;
        }
    }

    /// <summary>
    /// 检查当前是否已注册。
    /// </summary>
    public bool IsRegistered()
    {
        if (!IsSupported) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(VerbKeyPath_File);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

#pragma warning disable CA1416
    private static void WriteVerb(string keyPath, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        if (key is null) throw new InvalidOperationException($"无法创建注册表项：HKCU\\{keyPath}");
        key.SetValue("", VerbDisplayName);
        key.SetValue("Icon", exePath); // 让右键菜单旁边能显示我们的图标

        using var cmdKey = key.CreateSubKey("command");
        if (cmdKey is null) throw new InvalidOperationException("无法创建 command 子项");
        cmdKey.SetValue("", $"\"{exePath}\" --identify \"%1\"");
    }

    private static void DeleteVerb(string keyPath)
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DeleteVerb 失败（忽略）：{Path}", keyPath);
        }
    }
#pragma warning restore CA1416

    private static string? GetExePath()
    {
        // .NET 单文件发布时 Process.MainModule.FileName 给出 exe 路径
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;

        // Fallback：AppContext.BaseDirectory 下找同名 exe
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "NineKgTools.Desktop.exe");
        if (File.Exists(candidate)) return candidate;
        return null;
    }
}
