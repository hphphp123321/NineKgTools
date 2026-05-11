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
    /// <summary>落盘路径——Load 后由静态方法塞值。`[JsonIgnore]` 不参与序列化。</summary>
    [JsonIgnore]
    private string? _filePath;

    [JsonIgnore]
    private readonly object _saveLock = new();

    [JsonIgnore]
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

    /// <summary>
    /// 媒体详情页是否使用"封面玻璃材质"沉浸式背景（默认 false，保守不变现有 Mica 体验）。
    /// 开启后 MediaDetailContent 在最外层套 5 层 Z-stack：原 Mica → 模糊封面 → 暗化层 → vignette → 内容层。
    /// </summary>
    public bool UseGlassBackground { get; set; } = false;

    /// <summary>UseGlassBackground 变化广播——MediaDetailViewModel 订阅实时切换 UI。
    /// event 默认不被 System.Text.Json 序列化，无需 JsonIgnore（特性不适用于 event 声明）</summary>
    public event EventHandler? UseGlassBackgroundChanged;

    /// <summary>由 SettingsViewModel 在用户 toggle 时调用：写入 + 持久化 + 通知订阅者</summary>
    public void SetUseGlassBackground(bool value)
    {
        if (UseGlassBackground == value) return;
        UseGlassBackground = value;
        RequestSave();
        UseGlassBackgroundChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 无参构造——给 System.Text.Json 反序列化用（构造参数必须能映射到 public property，
    /// 我们的 _filePath 是 JsonIgnore 的字段，所以反序列化时必须走无参构造）。
    /// </summary>
    public DesktopPreferences() { }

    /// <summary>
    /// 同步加载——构造在启动期，首次读为 null 时返回默认实例。
    /// </summary>
    public static DesktopPreferences Load(string dataDir)
    {
        var filePath = Path.Combine(dataDir, "desktop-preferences.json");
        DesktopPreferences? loaded = null;
        try
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                loaded = JsonSerializer.Deserialize<DesktopPreferences>(json, JsonOpts);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DesktopPreferences 加载失败，回退默认值：{Path}", filePath);
        }
        var result = loaded ?? new DesktopPreferences();
        result._filePath = filePath;
        result.WindowStates ??= new Dictionary<string, WindowState>();
        return result;
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
        if (string.IsNullOrEmpty(_filePath))
        {
            Log.Warning("DesktopPreferences._filePath 未初始化，跳过落盘（应通过 Load 获取实例）");
            return;
        }
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
