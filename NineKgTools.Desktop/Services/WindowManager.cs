using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.ViewModels.Pages;
using NineKgTools.Desktop.Views.Windows;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 管理所有非主窗子窗（媒体详情、独立任务进度等）。
/// 同一媒体重复请求时 Activate 现有窗口（不重复开），主窗关闭时 CloseAll。
/// </summary>
public sealed class WindowManager
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<string, Window> _openWindows = new();

    public WindowManager(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>打开媒体详情独立窗口；同一 mediaId 二次调用则把现有窗口拉到前台</summary>
    public void OpenMediaDetail(int mediaId)
    {
        var key = $"media:{mediaId}";
        if (_openWindows.TryGetValue(key, out var existing) && existing.IsVisible)
        {
            existing.Activate();
            return;
        }

        var vm = _services.GetRequiredService<MediaDetailViewModel>();
        var window = new MediaDetailWindow { DataContext = vm };

        // 异步加载媒体数据（窗口已显示则不阻塞）
        _ = vm.LoadAsync(mediaId);

        window.Closed += (_, _) =>
        {
            _openWindows.Remove(key);
            Log.Debug("MediaDetail 窗口已关闭：mediaId={Id}", mediaId);
        };

        _openWindows[key] = window;
        window.Show();
    }

    /// <summary>主窗关闭时调用，清理所有子窗</summary>
    public void CloseAll()
    {
        foreach (var w in _openWindows.Values.ToList())
        {
            try { w.Close(); }
            catch (Exception ex) { Log.Warning(ex, "关闭子窗失败"); }
        }
        _openWindows.Clear();
    }
}
