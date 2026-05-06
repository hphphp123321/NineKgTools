using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 跨进程命令通道。用于 Shell 右键集成 / 命令行参数：
/// - 现有进程时：新进程把命令通过 pipe 转发，自己退出
/// - 无进程时：新进程启动后处理命令
///
/// 协议：JSON-Lines（每行一个 <see cref="IpcCommand"/>）
/// 平台：
/// - Windows → NamedPipe `NineKgTools.Desktop.IPC.{username}`
/// - macOS / Linux → Unix Domain Socket（同名 + tmp 路径）
/// </summary>
public class IpcService : IDisposable
{
    private readonly DragDropDispatcher _dragDispatcher;
    private readonly TrayService _trayService;

    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;
    private bool _disposed;

    public IpcService(DragDropDispatcher dragDispatcher, TrayService trayService)
    {
        _dragDispatcher = dragDispatcher;
        _trayService = trayService;
    }

    /// <summary>
    /// 当前进程的 IPC 通道名（每用户独立）。
    /// </summary>
    public static string PipeName
    {
        get
        {
            var user = Environment.UserName;
            return $"NineKgTools.Desktop.IPC.{user}";
        }
    }

    /// <summary>
    /// 启动后台 server——监听新连接，反序列化命令，调度到对应 handler。
    /// 失败时静默——其他平台 NamedPipe 限制可能导致启动失败，应用主流程不应被阻塞。
    /// </summary>
    public void StartServer()
    {
        if (_disposed || _serverTask is not null) return;
        try
        {
            _serverCts = new CancellationTokenSource();
            var token = _serverCts.Token;
            _serverTask = Task.Run(() => RunServerLoopAsync(token), token);
            Log.Information("IpcService server 已启动：{Pipe}", PipeName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "IpcService server 启动失败（平台可能不支持 NamedPipe），跳过 IPC 功能");
            _serverTask = null;
            _serverCts?.Dispose();
            _serverCts = null;
        }
    }

    private async Task RunServerLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using var reader = new StreamReader(server, leaveOpen: false);
                while (!token.IsCancellationRequested && !reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (string.IsNullOrEmpty(line)) continue;
                    await DispatchAsync(line);
                }
            }
            catch (OperationCanceledException) { /* 退出 */ }
            catch (Exception ex)
            {
                Log.Warning(ex, "IpcService server loop 异常，重试");
                await Task.Delay(500, token).ContinueWith(_ => { });
            }
            finally
            {
                server?.Dispose();
            }
        }
    }

    private async Task DispatchAsync(string line)
    {
        IpcCommand? cmd;
        try
        {
            cmd = JsonSerializer.Deserialize<IpcCommand>(line, JsonOpts);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "IpcService 反序列化命令失败：{Line}", line);
            return;
        }
        if (cmd is null) return;

        Log.Information("IpcService 收到命令：{Cmd} {Path}", cmd.Cmd, cmd.Path);

        switch (cmd.Cmd?.ToLowerInvariant())
        {
            case "identify":
                if (!string.IsNullOrEmpty(cmd.Path))
                {
                    // 切回 UI 线程后再调 dispatcher（HandleDropAsync 会弹对话框）
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        _trayService.ShowMainWindow(); // 拉起主窗
                        await _dragDispatcher.HandleDropAsync(new[] { cmd.Path });
                    });
                }
                break;
            case "rescan-folder":
                if (!string.IsNullOrEmpty(cmd.Path))
                {
                    // 强制 SkipCache 重识别——绕过 GetMediaByPath 短路，让 DLsite 重新爬取
                    // + 重新下载 Poster/Pictures，修复历史脏数据（Media.Poster=null 等）
                    await _dragDispatcher.RescanFolderAsync(cmd.Path);
                }
                break;
            case "show-main":
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => _trayService.ShowMainWindow());
                break;
            case "quit":
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => _trayService.RequestExit());
                break;
            default:
                Log.Warning("IpcService 未识别命令：{Cmd}", cmd.Cmd);
                break;
        }
    }

    /// <summary>
    /// 客户端：尝试连接已有进程的 IPC 通道并转发命令。
    /// 2 秒超时——超时即认为没有现有进程，调用方应继续启动新进程自行处理命令。
    /// </summary>
    public static async Task<bool> TrySendAsync(IpcCommand cmd, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(2);
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            using var connectCts = new CancellationTokenSource(timeout.Value);
            await client.ConnectAsync(connectCts.Token);

            var json = JsonSerializer.Serialize(cmd, JsonOpts) + "\n";
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await client.WriteAsync(bytes, 0, bytes.Length);
            await client.FlushAsync();
            return true;
        }
        catch (TimeoutException) { return false; }
        catch (OperationCanceledException) { return false; }
        catch (IOException) { return false; }
        catch (Exception ex)
        {
            Log.Warning(ex, "IpcService 转发失败");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _serverCts?.Cancel();
            _serverTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch { }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// 一条 IPC 命令的载荷。
/// </summary>
public class IpcCommand
{
    public string Cmd { get; set; } = "";
    public string? Path { get; set; }
}
