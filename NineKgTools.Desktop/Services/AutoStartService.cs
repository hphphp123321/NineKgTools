using System.Runtime.InteropServices;
#pragma warning disable CA1416 // 平台特定 API 警告——本类整体仅 Windows 调用
using Microsoft.Win32;
#pragma warning restore CA1416
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 开机自启动（HKEY_CURRENT_USER\...\Run 注册表方式，无需 UAC 提权）。
/// 启用后系统登录时会以 <c>"&lt;exe&gt;" --autostart</c> 拉起本进程——
/// <c>--autostart</c> 让进程静默隐藏到托盘启动（不弹主窗），见 <see cref="Program"/> 的 StartHidden 处理。
/// 非 Windows 平台所有方法 no-op + 返回 false（与 <see cref="ShellIntegrationService"/> 一致）。
/// </summary>
public class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "NineKgTools";

    public static bool IsSupported => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>注册开机自启（HKCU Run）。失败返回 false（不抛异常）。</summary>
    public bool Enable()
    {
        if (!IsSupported)
        {
            Log.Information("AutoStartService.Enable: 非 Windows 平台，跳过");
            return false;
        }

        try
        {
            var exePath = GetExePath();
            if (string.IsNullOrEmpty(exePath))
            {
                Log.Warning("AutoStartService.Enable: 无法解析当前 exe 路径");
                return false;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) throw new InvalidOperationException($"无法打开注册表项：HKCU\\{RunKeyPath}");
            // --autostart：登录时静默隐藏到托盘启动（不弹主窗）
            key.SetValue(ValueName, $"\"{exePath}\" --autostart");

            Log.Information("开机自启已注册到 HKCU Run：{Exe}", exePath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AutoStartService.Enable 失败");
            return false;
        }
    }

    /// <summary>取消开机自启（删除 HKCU Run 值）。失败返回 false。</summary>
    public bool Disable()
    {
        if (!IsSupported) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            // 值不存在视为已禁用——删除是幂等的
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
            Log.Information("开机自启已从 HKCU Run 移除");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AutoStartService.Disable 失败");
            return false;
        }
    }

    /// <summary>检查当前是否已注册开机自启。</summary>
    public bool IsEnabled()
    {
        if (!IsSupported) return false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetExePath()
    {
        // .NET 单文件发布时 Environment.ProcessPath 给出 exe 路径
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)) return path;

        // Fallback：AppContext.BaseDirectory 下找同名 exe
        var baseDir = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDir, "NineKgTools.Desktop.exe");
        if (File.Exists(candidate)) return candidate;
        return null;
    }
}
