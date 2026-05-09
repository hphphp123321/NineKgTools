using Avalonia;
using Avalonia.Controls;

namespace NineKgTools.Desktop.Converters;

/// <summary>
/// 给 <see cref="TextBlock"/> 加 <c>helpers:HtmlText.Source="{Binding Description}"</c>
/// 即可用 <see cref="HtmlInlinesParser"/> 把 HTML 字符串渲染成富文本（&lt;p&gt; / &lt;br&gt; /
/// &lt;strong&gt; / &lt;em&gt; / &lt;ul&gt; 等）。
///
/// 不能直接 bind <c>TextBlock.Inlines</c>——它是 InlineCollection 而非 string，
/// Avalonia 12 的 binding system 不支持这种 attached collection 替换。所以走附加属性
/// + PropertyChanged 回调 -> 重建 Inlines 的模式。
///
/// 用法（axaml）：
/// <code>
/// xmlns:conv="using:NineKgTools.Desktop.Converters"
///
/// &lt;TextBlock conv:HtmlText.Source="{Binding Description}"
///            FontSize="13"
///            TextWrapping="Wrap" /&gt;
/// </code>
///
/// 与现有 <c>Text="{Binding ...}"</c> 互斥：set 了 HtmlText.Source 之后，
/// 不要再同时 set Text，否则 Text 会清空 Inlines。
/// </summary>
public static class HtmlText
{
    public static readonly AttachedProperty<string?> SourceProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>(
            "Source", typeof(HtmlText));

    static HtmlText()
    {
        SourceProperty.Changed.AddClassHandler<TextBlock>(OnSourceChanged);
    }

    private static void OnSourceChanged(TextBlock target, AvaloniaPropertyChangedEventArgs e)
    {
        target.Inlines?.Clear();

        var html = e.NewValue as string;
        if (string.IsNullOrEmpty(html)) return;

        var inlines = HtmlInlinesParser.Parse(html);
        if (target.Inlines is null) return;
        foreach (var inline in inlines)
            target.Inlines.Add(inline);
    }

    public static void SetSource(TextBlock target, string? value)
        => target.SetValue(SourceProperty, value);

    public static string? GetSource(TextBlock target)
        => target.GetValue(SourceProperty);
}
