using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using NineKgTools.Desktop.Services;
using Serilog;

namespace NineKgTools.Desktop.Converters;

/// <summary>
/// AXAML 用：把 string（图片名） → Bitmap，走 ImageCacheService LRU 缓存。
///
/// 用法：
///     <Image Source="{Binding CoverImageName, Converter={StaticResource BitmapAsyncConverter}}" />
///
/// Convert 是同步签名，异步加载发起后立刻返回 null（UI 显示空），
/// 加载完成后通过 Dispatcher 触发绑定刷新——但 Avalonia binding 不会自动重读。
/// 实际使用时配合 ViewModel 里 <see cref="ObservableProperty"/> 异步赋值的 Bitmap 属性更直接，
/// 这个 Converter 只用于"绑定字符串 ImageName 到 Image 控件"的简便场景。
/// </summary>
public sealed class BitmapAsyncConverter : IValueConverter
{
    public static BitmapAsyncConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string imageName || string.IsNullOrWhiteSpace(imageName))
            return null;

        var cache = Program.Services?.GetService<ImageCacheService>();
        if (cache is null)
        {
            Log.Warning("BitmapAsyncConverter: ImageCacheService 未在 DI 注册");
            return null;
        }

        // 已命中即同步返回；未命中由调用方走异步路径
        // （此 Converter 不阻塞 UI 线程，未命中返回 null 让 UI 显示 placeholder）
        var task = cache.GetOrLoadAsync(imageName);
        if (task.IsCompletedSuccessfully)
        {
            return task.Result;
        }

        // 异步等待但不阻塞 UI——通过 Dispatcher 触发，但这里返回 null 让控件显示 fallback
        // 真实的"加载好后自动更新"由 VM 层用 [ObservableProperty] Bitmap 实现
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted) Log.Warning(t.Exception, "BitmapAsyncConverter 加载图片异常");
        });
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
