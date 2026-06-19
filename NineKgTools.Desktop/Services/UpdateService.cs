using System.Reflection;
using Serilog;
using Velopack;
using Velopack.Sources;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 桌面端自动更新（Velopack）。只在 Velopack 安装版生效——
/// dev 运行 / portable 单文件下 <see cref="IsSupported"/> = false，所有方法 no-op，
/// 不会误弹更新提示。更新源 = GitHub Releases（CI 用 <c>vpk upload github</c> 发布）。
///
/// 用法：启动静默 <see cref="CheckAsync"/>；命中后调 <see cref="DownloadAndApplyAsync"/>
/// （下载完成会立即退出并重启到新版，方法不会正常返回）。
/// </summary>
public class UpdateService
{
    /// <summary>更新源仓库——与 CI <c>vpk upload github --repoUrl</c> 一致。</summary>
    public const string RepoUrl = "https://github.com/hphphp123321/NineKgTools";

    /// <summary>
    /// 本地调试开关：设了此环境变量（指向本地 Releases 目录或自托管 feed URL）就用它当更新源，
    /// 免去每次都推 GitHub。仍需先用 <c>vpk</c> 真装一次应用（否则 <see cref="IsSupported"/> 仍 false）。
    /// 例：<c>set NINEKG_UPDATE_FEED=D:\NineKg\Releases</c>。
    /// </summary>
    private const string FeedEnvVar = "NINEKG_UPDATE_FEED";

    private UpdateManager? _mgr;

    private UpdateManager Manager => _mgr ??= BuildManager();

    private static UpdateManager BuildManager()
    {
        var localFeed = Environment.GetEnvironmentVariable(FeedEnvVar);
        if (!string.IsNullOrWhiteSpace(localFeed))
        {
            // UpdateManager(string) 自动识别本地目录 / URL feed——本地快速迭代用
            Log.Information("更新源使用本地 feed（{EnvVar}）：{Feed}", FeedEnvVar, localFeed);
            return new UpdateManager(localFeed);
        }
        return new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
    }

    /// <summary>当前是否为 Velopack 安装版（决定更新功能是否可用）。dev/portable 下为 false。</summary>
    public bool IsSupported
    {
        get
        {
            try { return Manager.IsInstalled; }
            catch (Exception ex)
            {
                Log.Debug(ex, "UpdateManager.IsInstalled 探测失败，按不支持处理");
                return false;
            }
        }
    }

    /// <summary>显示用当前版本号。安装版取 Velopack 当前版本；否则回退程序集版本（dev 也能看到数字）。</summary>
    public string CurrentVersionText
    {
        get
        {
            try
            {
                if (Manager.IsInstalled && Manager.CurrentVersion is { } v)
                    return v.ToString();
            }
            catch { /* 探测失败回退程序集版本 */ }

            var asm = Assembly.GetEntryAssembly()?.GetName().Version
                      ?? typeof(UpdateService).Assembly.GetName().Version;
            return asm is null ? "0.0.0" : $"{asm.Major}.{asm.Minor}.{asm.Build}";
        }
    }

    /// <summary>检查是否有新版本。无更新 / 不支持 / 网络失败都返回 null（不抛，调用方静默处理）。</summary>
    public async Task<UpdateInfo?> CheckAsync()
    {
        if (!IsSupported) return null;
        try
        {
            return await Manager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "检查更新失败");
            return null;
        }
    }

    /// <summary>下载指定更新并应用 + 重启。应用阶段会立即退出进程，方法通常不会正常返回。</summary>
    public async Task DownloadAndApplyAsync(UpdateInfo info, Action<int>? onProgress = null)
    {
        await Manager.DownloadUpdatesAsync(info, onProgress);
        // 退出当前进程 → Update.exe 接管落地新版 → 重启
        Manager.ApplyUpdatesAndRestart(info.TargetFullRelease);
    }
}
