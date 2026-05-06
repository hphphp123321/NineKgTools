using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 子窗共享行为：Ctrl+W 关闭、Ctrl+T 切换置顶、位置记忆。
/// 在每个子窗 ctor 调 <see cref="EnableChildWindowFeatures"/> 一次性接好。
/// </summary>
public static class WindowExtensions
{
    public static void EnableChildWindowFeatures(this Window window, string stateKey)
    {
        // 位置记忆
        try
        {
            Program.Services?.GetService<WindowStateService>()?.Attach(window, stateKey);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EnableChildWindowFeatures: WindowStateService.Attach 失败 key={Key}", stateKey);
        }

        // Ctrl+W 关自己 + Ctrl+T 切置顶
        window.AddHandler(InputElement.KeyDownEvent, (object? _, KeyEventArgs e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            switch (e.Key)
            {
                case Key.W:
                    window.Close();
                    e.Handled = true;
                    break;
                case Key.T:
                    window.Topmost = !window.Topmost;
                    e.Handled = true;
                    break;
            }
        }, handledEventsToo: false);
    }
}
