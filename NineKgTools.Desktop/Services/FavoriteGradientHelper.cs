using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 把"收藏夹名字"映射成稳定的三色 chip 配色——给 MediaDetailWindow 顶部的收藏夹 pill 列上色。
/// 风格与"分类 chip"完全一致：浅色 fill + 实色 1px border + accent 文字色，无渐变 / 无阴影。
///
/// 算法：hash(name) → HSL hue（0-360），固定饱和度与亮度参数生成三色 SolidColorBrush。
/// 同名字保证同色（dictionary 缓存）；Light / Dark 主题各派生一套（暗色背景上要反转：
/// 深色 fill + 中等亮度 border + 浅色文字，反之亦然）。
///
/// 与"分类色"刻意区分：分类色固定 5 套（视频/音频/游戏/图片/文本），代表"客观属性"；
/// 收藏夹色随名字派生，代表"用户主观归属"——视觉系不应混淆但风格应统一（同 chip 模板）。
/// </summary>
public static class FavoriteGradientHelper
{
    /// <summary>三色 brush triplet：背景填充 / 边框 / 文字。整 record 持有的 brush 不可变，安全 cache。</summary>
    public sealed record FavoriteBrushes(IBrush Background, IBrush Border, IBrush Foreground);

    private static readonly Dictionary<(string name, bool dark), FavoriteBrushes> _cache = new();

    public static FavoriteBrushes Get(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            // fallback：中性灰
            return new FavoriteBrushes(
                new SolidColorBrush(Color.FromArgb(40, 128, 128, 128)),
                new SolidColorBrush(Color.FromRgb(140, 140, 140)),
                new SolidColorBrush(Color.FromRgb(96, 96, 96)));
        }

        var isDark = IsDarkTheme();
        var key = (name, isDark);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var hue = HashToHue(name);
        FavoriteBrushes triplet;
        if (isDark)
        {
            // 暗色主题：深 fill（带 alpha 浅淡） + 中亮度 border + 浅文字
            triplet = new FavoriteBrushes(
                Solid(hue, s: 0.40, l: 0.30, a: 0.35),
                Solid(hue, s: 0.55, l: 0.55),
                Solid(hue, s: 0.55, l: 0.78));
        }
        else
        {
            // 浅色主题：浅 fill（接近 BrandCategoryXxxFillBrush 的 alpha 0x20-0x30 质感） + 实色 border + 深文字
            triplet = new FavoriteBrushes(
                Solid(hue, s: 0.50, l: 0.92),
                Solid(hue, s: 0.55, l: 0.55),
                Solid(hue, s: 0.65, l: 0.32));
        }

        _cache[key] = triplet;
        return triplet;
    }

    private static SolidColorBrush Solid(double h, double s, double l, double a = 1.0)
    {
        var (r, g, b) = HslToRgb(h, s, l);
        var alpha = (byte)Math.Round(a * 255);
        return new SolidColorBrush(Color.FromArgb(alpha, r, g, b));
    }

    private static bool IsDarkTheme()
    {
        var variant = Application.Current?.ActualThemeVariant;
        return variant == ThemeVariant.Dark;
    }

    /// <summary>name 的稳定哈希 → 0-360 hue。用 FNV-1a 32 位避免不同 .NET 版本 String.GetHashCode 不稳定。</summary>
    private static double HashToHue(string name)
    {
        const uint fnvPrime = 16777619;
        const uint fnvOffset = 2166136261;
        uint h = fnvOffset;
        foreach (var c in name)
        {
            h ^= c;
            h *= fnvPrime;
        }
        return h % 360u;
    }

    /// <summary>HSL → RGB（0-255）。h ∈ [0,360]，s/l ∈ [0,1]。</summary>
    private static (byte r, byte g, byte b) HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r1, g1, b1;
        switch ((int)hp)
        {
            case 0: r1 = c; g1 = x; b1 = 0; break;
            case 1: r1 = x; g1 = c; b1 = 0; break;
            case 2: r1 = 0; g1 = c; b1 = x; break;
            case 3: r1 = 0; g1 = x; b1 = c; break;
            case 4: r1 = x; g1 = 0; b1 = c; break;
            default: r1 = c; g1 = 0; b1 = x; break;
        }
        double m = l - c / 2;
        return ((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
    }
}
