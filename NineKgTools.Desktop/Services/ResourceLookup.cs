using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// VM 侧按 key 解析应用资源（系统主题 brush / BrandResources 图标 Geometry）。
///
/// ⚠ 必须用 <see cref="ResourceNodeExtensions.TryGetResource(IResourceHost, object, Avalonia.Styling.ThemeVariant?, out object?)"/>
/// （Application 作为 IResourceHost 的扩展方法）——它会沿 Styles 链搜索，FluentAvalonia 的
/// SystemFillColorXxxBrush 等主题资源都挂在 theme Styles 里；
/// 直接调 `Application.Current.Resources.TryGetResource(...)` 只搜 App 级 ResourceDictionary
/// 本身 + MergedDictionaries，主题 brush 一律 miss 返回 null（症状：状态图标 / 状态文字隐形）。
/// </summary>
public static class ResourceLookup
{
    public static IBrush? Brush(string key)
    {
        var app = Application.Current;
        if (app is not null
            && app.TryGetResource(key, app.ActualThemeVariant, out var obj)
            && obj is IBrush b)
        {
            return b;
        }
        return null;
    }

    public static Geometry? Geometry(string key)
    {
        var app = Application.Current;
        if (app is not null
            && app.TryGetResource(key, app.ActualThemeVariant, out var obj)
            && obj is Geometry g)
        {
            return g;
        }
        return null;
    }
}
