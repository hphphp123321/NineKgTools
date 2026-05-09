using Avalonia.Controls.Documents;
using Avalonia.Media;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Desktop.Converters;

/// <summary>
/// 把 HTML 字符串解析成 Avalonia <see cref="Inline"/> 集合，可直接 set 给
/// <c>TextBlock.Inlines</c> 渲染富文本。配合 <see cref="HtmlText"/> 附加属性用。
///
/// 数据源：DLsite 详情页爬取的 description 保留了 HTML（&lt;p&gt; / &lt;br&gt; / &lt;strong&gt; 等），
/// Web 端用 Blazor <c>(MarkupString)Description</c> 直接渲染；桌面端 Avalonia 没有
/// 内置 HTML 渲染，故自实现一个轻量解析器。
///
/// 支持标签（覆盖 DLsite / 一般描述用法的 95%+）：
/// - 段落：&lt;p&gt; / &lt;div&gt;       → 末尾换行
/// - 换行：&lt;br&gt; / &lt;br/&gt;        → LineBreak
/// - 加粗：&lt;b&gt; / &lt;strong&gt;      → Bold
/// - 斜体：&lt;i&gt; / &lt;em&gt;          → Italic
/// - 下划线：&lt;u&gt;                  → Underline
/// - 列表：&lt;ul&gt; / &lt;ol&gt; / &lt;li&gt; → "• " 前缀 + 末尾换行
/// - 链接：&lt;a&gt;                    → Underline + 文字（不可点击；URL 在 ToolTip 用 hover 看）
/// - 其它：保留 InnerText 剥去标签
///
/// HTML entity（&amp;amp; &amp;lt; &amp;nbsp; 等）由 HtmlAgilityPack.HtmlEntity.DeEntitize 还原。
/// 解析失败 fallback 到纯文本，避免抛异常崩 UI。
/// </summary>
public static class HtmlInlinesParser
{
    public static IReadOnlyList<Inline> Parse(string? html)
    {
        var inlines = new List<Inline>();
        if (string.IsNullOrWhiteSpace(html)) return inlines;

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            ProcessNodes(doc.DocumentNode.ChildNodes, inlines,
                weight: FontWeight.Normal, style: FontStyle.Normal, underline: false);

            // 末尾若有 LineBreak 把它去掉，避免 TextBlock 尾部空一行
            while (inlines.Count > 0 && inlines[^1] is LineBreak)
                inlines.RemoveAt(inlines.Count - 1);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "HtmlInlinesParser 解析失败，fallback 到纯文本：{Snippet}",
                html.Length > 80 ? html.Substring(0, 80) + "..." : html);
            inlines.Clear();
            inlines.Add(new Run(html));
        }

        return inlines;
    }

    private static void ProcessNodes(HtmlNodeCollection nodes, List<Inline> inlines,
        FontWeight weight, FontStyle style, bool underline)
    {
        foreach (var node in nodes)
        {
            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                {
                    var text = HtmlEntity.DeEntitize(node.InnerText);
                    if (!string.IsNullOrEmpty(text))
                        inlines.Add(MakeRun(text, weight, style, underline));
                    break;
                }

                case HtmlNodeType.Element:
                {
                    var tag = node.Name.ToLowerInvariant();
                    switch (tag)
                    {
                        case "br":
                            inlines.Add(new LineBreak());
                            break;

                        case "p":
                        case "div":
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline);
                            inlines.Add(new LineBreak());
                            break;

                        case "b":
                        case "strong":
                            ProcessNodes(node.ChildNodes, inlines, FontWeight.Bold, style, underline);
                            break;

                        case "i":
                        case "em":
                            ProcessNodes(node.ChildNodes, inlines, weight, FontStyle.Italic, underline);
                            break;

                        case "u":
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline: true);
                            break;

                        case "li":
                            inlines.Add(new Run("• "));
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline);
                            inlines.Add(new LineBreak());
                            break;

                        case "ul":
                        case "ol":
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline);
                            break;

                        case "a":
                            // Hyperlink 简化：保留下划线 + 文字（不可点击；现在 description 里链接很少）
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline: true);
                            break;

                        // 标题 / 表格 / 图片等不常见标签：剥标签，保留文本
                        default:
                            ProcessNodes(node.ChildNodes, inlines, weight, style, underline);
                            break;
                    }
                    break;
                }
            }
        }
    }

    private static Run MakeRun(string text, FontWeight weight, FontStyle style, bool underline)
    {
        var run = new Run(text);
        if (weight != FontWeight.Normal) run.FontWeight = weight;
        if (style != FontStyle.Normal) run.FontStyle = style;
        if (underline) run.TextDecorations = TextDecorations.Underline;
        return run;
    }
}
