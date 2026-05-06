using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Core.Hosting;
using NineKgTools.Desktop.Services;
using NineKgTools.Desktop.ViewModels;
using NineKgTools.Desktop.Views;
using Serilog;

namespace NineKgTools.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = Program.Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            // 主窗显示后再触发 AfterStartup（拉起文件夹监控、定时任务），避免阻塞 UI 初始化
            window.Opened += async (_, _) =>
            {
                try
                {
                    await vm.InitializeAsync();
                    await AppBootstrap.RunAfterStartupAsync(Program.Services);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "应用启动后初始化失败");
                }
            };

            // 主窗关闭时把所有子窗（媒体详情等）一并关闭
            window.Closing += (_, _) =>
            {
                try { Program.Services.GetService<WindowManager>()?.CloseAll(); }
                catch (Exception ex) { Log.Warning(ex, "关闭子窗时异常"); }
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
