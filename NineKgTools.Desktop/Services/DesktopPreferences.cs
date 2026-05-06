using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Threading;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 桌面端独有的 UI 偏好持久化（不进 config.yaml，与 Web 端解耦）。
/// 落盘：`{dataDir}/desktop-preferences.json`。
/// 读：进程启动时同步加载；写：500ms 防抖，UI 线程触发后台 save。
/// </summary>
public class DesktopPreferences
{
    private readonly string _filePath;
    private readonly object _saveLock = new();
    private CancellationTokenSource? _saveDebounceCts;

    /// <summary>
    /// 关闭主窗时的行为。
    /// </summary>
    public CloseAction CloseAction { get; set; } = CloseAction.MinimizeToTray;

    /// <summary>
    /// 主题选择（System / Light / Dark）。null = 不持久化。
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// 是否已经向用户提示过"应用仍在托盘运行"InfoBar——只提示一次。
    /// </summary>
    public bool TrayHintShown { get; set; }

    /// <summary>
    /// 是否注册了 Windows 资源管理器右键菜单 verb。
    /// 仅 Windows 有效，其他平台保持 false。
    /// </summary>
    public bool ShellIntegrationRegistered { get; set; }

    /// <summary>
    /// 各窗口的 size + position 记忆。key 形如 "main" / "media:42" / "diagnostics"。
    /// </summary>
    public Dictionary<string, WindowState> WindowStates { get; set; } = new();

    public DesktopPreferences(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// 同步加载——构造在启动期，首次读为 null 时返回默认实例。
    /// </summary>
    public static DesktopPreferences Load(string dataDir)
    {
        var filePath = Path.Combine(dataDir, "desktop-preferences.json");
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var loaded = JsonSerializer.Deserialize<DesktopPreferences>(json, JsonOpts);
                if (loaded is not null)
                {
                    // 反序列化的对象 _filePath 是空，需要补上
                    return new DesktopPreferences(filePath)
                    {
                        CloseAction = loaded.CloseAction,
                        Theme = loaded.Theme,
                        TrayHintShown = loaded.TrayHintShown,
                        ShellIntegrationRegistered = loaded.ShellIntegrationRegistered,
                        WindowStates = loaded.WindowStates ?? new Dictionary<string, WindowState>(),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopPreferences 加载失败，回退默认值：{Path}", filePath);
        }
        return new DesktopPreferences(filePath);
    }

    /// <summary>500ms 防抖落盘。</summary>
    public void RequestSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var token = _saveDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;
                await SaveAsync();
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    /// <summary>立即落盘（用于退出前等场景）。</summary>
    public async Task SaveAsync()
    {
        try
        {
            string json;
            lock (_saveLock)
            {
                json = JsonSerializer.Serialize(this, JsonOpts);
            }
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopPreferences 保存失败：{Path}", _filePath);
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}

/// <summary>
/// 关闭主窗时的行为枚举。
/// </summary>
public enum CloseAction
{
    /// <summary>最小化到系统托盘，应用继续在后台运行。</summary>
    MinimizeToTray,

    /// <summary>真正退出应用进程。</summary>
    Exit,
}

/// <summary>
/// 单个窗口的位置 + 大小记忆。
/// </summary>
public class WindowState
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>窗口是否最大化。最大化的窗口 X/Y/Width/Height 仍记录最大化前的值，方便恢复。</summary>
    public bool IsMaximized { get; set; }
}
