using Avalonia;
using Avalonia.Controls;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 帮 Window 把 size + position 持久化到 <see cref="DesktopPreferences.WindowStates"/>。
/// 用法：在 Window 构造里调 <see cref="Attach"/>(window, key)，离开时自动落盘；下次同 key 的窗口 Show 前会被还原。
/// </summary>
public class WindowStateService
{
    private readonly DesktopPreferences _preferences;

    public WindowStateService(DesktopPreferences preferences)
    {
        _preferences = preferences;
    }

    /// <summary>
    /// 把 window 的 size/position 持久化绑定到 key。窗口打开时还原；移动 / 调整大小 / 关闭时保存。
    /// </summary>
    public void Attach(Window window, string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        // 还原（在 Opened 之前；用 Loaded 时机最合适）
        window.Opened += (_, _) => Restore(window, key);
        window.Closing += (_, _) => Save(window, key);

        // 窗口在拖动 / 调整大小后也保存（防止用户没关窗就崩溃，下次回到上次位置）
        window.PositionChanged += (_, _) => SaveDeferred(window, key);
        window.GetObservable(Window.ClientSizeProperty).Subscribe(new SizeObserver(this, window, key));
    }

    private void Restore(Window window, string key)
    {
        if (!_preferences.WindowStates.TryGetValue(key, out var state)) return;
        try
        {
            // 检查屏幕边界——避免恢复到已断开的副屏外
            if (IsOnVisibleScreen(window, state.X, state.Y, state.Width, state.Height))
            {
                window.Position = new PixelPoint((int)state.X, (int)state.Y);
                window.Width = Math.Max(window.MinWidth, state.Width);
                window.Height = Math.Max(window.MinHeight, state.Height);
            }
            if (state.IsMaximized)
            {
                window.WindowState = Avalonia.Controls.WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WindowStateService 还原失败：key={Key}", key);
        }
    }

    private void Save(Window window, string key)
    {
        try
        {
            var isMax = window.WindowState == Avalonia.Controls.WindowState.Maximized;
            // 最大化时记录最大化前的尺寸，下次还原后还能取消最大化恢复
            var existing = _preferences.WindowStates.TryGetValue(key, out var prev) ? prev : null;

            var state = new WindowState
            {
                X = isMax && existing != null ? existing.X : window.Position.X,
                Y = isMax && existing != null ? existing.Y : window.Position.Y,
                Width = isMax && existing != null ? existing.Width : window.Width,
                Height = isMax && existing != null ? existing.Height : window.Height,
                IsMaximized = isMax,
            };
            _preferences.WindowStates[key] = state;
            _preferences.RequestSave();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WindowStateService 保存失败：key={Key}", key);
        }
    }

    private void SaveDeferred(Window window, string key) => Save(window, key);

    private static bool IsOnVisibleScreen(Window window, double x, double y, double w, double h)
    {
        try
        {
            foreach (var screen in window.Screens.All)
            {
                var b = screen.Bounds;
                // 至少有 50px 在某个屏幕内
                var overlapX = Math.Min(b.X + b.Width, x + w) - Math.Max(b.X, x);
                var overlapY = Math.Min(b.Y + b.Height, y + h) - Math.Max(b.Y, y);
                if (overlapX > 50 && overlapY > 50) return true;
            }
        }
        catch { /* Screens API 在某些平台/虚拟机会异常 */ }
        return false;
    }

    private sealed class SizeObserver : IObserver<Size>
    {
        private readonly WindowStateService _svc;
        private readonly Window _window;
        private readonly string _key;

        public SizeObserver(WindowStateService svc, Window window, string key)
        {
            _svc = svc;
            _window = window;
            _key = key;
        }

        public void OnNext(Size value) => _svc.SaveDeferred(_window, _key);
        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
