using System.Globalization;
using Avalonia.Data.Converters;
using NineKgTools.Core.Models.Categories;
using NineKgTools.Desktop.Services;

namespace NineKgTools.Desktop.Converters;

/// <summary>把 TopCategory enum 转成对应的 BrandCategoryXxxBrush（accent 色）</summary>
public sealed class CategoryAccentBrushConverter : IValueConverter
{
    public static CategoryAccentBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TopCategory cat) return TopCategoryStyles.ResolveAccentBrush(cat);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>把 TopCategory enum 转成对应的 BrandCategoryXxxFillBrush（带 alpha 浅版）</summary>
public sealed class CategoryFillBrushConverter : IValueConverter
{
    public static CategoryFillBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TopCategory cat) return TopCategoryStyles.ResolveFillBrush(cat);
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
