using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// 把 "SystemFillColorAttentionBrush" / "SystemFillColorSuccessBrush" 这样的资源 key
/// 解析成实际 IBrush。在 IdentificationProgressDialog 的 attempt 行用——因为每行状态色
/// 不同，无法在 AXAML 里写死 DynamicResource。
/// </summary>
public sealed class BrushKeyConverter : IValueConverter
{
    public static readonly BrushKeyConverter Instance = new();

    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrEmpty(key))
            return Brushes.Gray;

        if (Application.Current?.Resources.TryGetResource(
                key,
                Application.Current.ActualThemeVariant,
                out var resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}

/// <summary>int → bool（>0）。WPF 风格 Count 转 Visibility 的替代。</summary>
public sealed class GreaterThanZeroConverter : IValueConverter
{
    public static readonly GreaterThanZeroConverter Instance = new();

    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => value is int i && i > 0;

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
