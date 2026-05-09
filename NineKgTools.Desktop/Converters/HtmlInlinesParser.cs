using System.Diagnostics;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HtmlAgilityPack;
using Serilog;

namespace NineKgTools.Desktop.Converters;

/// <summary>
/// 把 HTML 字符串解析成 Avalonia <see cref="Inline"/> 集合，可直接 set 给
/// <c>TextBlock.Inlines</c> 渲染富文本。配合 <see cref="HtmlText"/> 附加属性用。
///
/// 数据源：DLsite 详情页爬取的 description 保留了 HTML（&lt;p&gt; / &lt;br&gt; /
/// &lt;strong&gt; / &lt;img&gt; / &lt;a&gt; 等），Web 端用 Blazor (MarkupString) 直接渲染；
/// 桌面端 Avalonia 12 没有内置 HTML 渲染，自实现一个轻量解析器。
///
/// 支持标签：
/// - 段落 &lt;p&gt; / &lt;div&gt;            末尾 LineBreak
/// - 换行 &lt;br&gt;                     LineBreak
/// - 加粗 &lt;b&gt; / &lt;strong&gt;          Run.FontWeight=Bold
/// - 斜体 &lt;i&gt; / &lt;em&gt;              Run.FontStyle=Italic
/// - 下划线 &lt;u&gt;                    Run.TextDecorations=Underline
/// - 列表 &lt;ul&gt;/&lt;ol&gt;/&lt;li&gt;          "• " 前缀 + LineBreak
/// - 链接 &lt;a href="..."&gt;           InlineUIContainer + TextBlock + PointerPressed → 默认浏览器
/// - 图片 &lt;img src="..."&gt;          InlineUIContainer + Image，HTTP 异步加载（max 480x320）
/// - 其它 / 不识别                  剥标签保留 InnerText
/// - HTML entity                  HtmlEntity.DeEntitize 还原
///
/// 解析失败 fallback 到纯文本，避免抛异常崩 UI。
/// </summary>
public static class HtmlInlinesParser
{
    /// <summary>
    /// 共享 HttpClient——HtmlInlinesParser 是 static 类，每次 parse 复用同一连接池。
    /// 10s 超时（DLsite 图片 CDN 应当 1-2s 出来；超时直接放弃，UI 上图位置空白）。
    /// </summary>
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>HTML inline 图片显示尺寸上限（CSS box，与原图分辨率无关，会缩放）。</summary>
    private const double MaxImageWidth = 480;
    private const double MaxImageHeight = 320;

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

            // 末尾若有连续 LineBreak 把它去掉，避免 TextBlock 尾部空一行
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
                        {
                            var href = node.GetAttributeValue("href", "")?.Trim();
                            var text = HtmlEntity.DeEntitize(node.InnerText) ?? "";
                            if (!string.IsNullOrEmpty(href) && IsSafeUrl(href))
                                inlines.Add(MakeHyperlink(text, href));
                            else
                                ProcessNodes(node.ChildNodes, inlines, weight, style, underline: true);
                            break;
                        }

                        case "img":
                        {
                            var src = node.GetAttributeValue("src", "")?.Trim();
                            if (!string.IsNullOrEmpty(src) && IsSafeUrl(src))
                            {
                                // 图片前后各加 LineBreak 让它独占一行——inline 巨图夹在文字里很丑。
                                // 末尾若已有 LineBreak 就不再加，避免双倍空行。
                                if (inlines.Count > 0 && inlines[^1] is not LineBreak)
                                    inlines.Add(new LineBreak());
                                var alt = node.GetAttributeValue("alt", "");
                                inlines.Add(MakeImage(src, alt));
                                inlines.Add(new LineBreak());
                            }
                            break;
                        }

                        default:
                            // 标题 / 表格 / 不识别等：剥标签，保留文本
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

    /// <summary>
    /// &lt;a href="..."&gt; → 蓝色 + 下划线的 TextBlock，PointerPressed 用系统默认浏览器打开。
    /// 包在 InlineUIContainer 才能放进 TextBlock.Inlines（Inline 子类型本身没有 pointer events）。
    /// </summary>
    private static Inline MakeHyperlink(string text, string href)
    {
        var displayText = string.IsNullOrEmpty(text) ? href : text;
        var tb = new TextBlock
        {
            Text = displayText,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            Foreground = GetAccentBrush(),
        };
        ToolTip.SetTip(tb, href);

        tb.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(tb).Properties.IsLeftButtonPressed) return;
            OpenUrl(href);
            e.Handled = true;
        };

        return new InlineUIContainer
        {
            Child = tb,
            BaselineAlignment = BaselineAlignment.Center,
        };
    }

    /// <summary>
    /// &lt;img src="..."&gt; → InlineUIContainer + Image，HttpClient 后台异步加载，
    /// 加载完成回 UI 线程 set Source。失败静默（图位置保持空 Image，CSS box 仍占位）。
    /// </summary>
    private static Inline MakeImage(string src, string alt)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, // 小图不放大避免模糊
            MaxWidth = MaxImageWidth,
            MaxHeight = MaxImageHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (!string.IsNullOrEmpty(alt))
            ToolTip.SetTip(image, alt);

        _ = LoadImageAsync(src, image);

        return new InlineUIContainer
        {
            Child = image,
            BaselineAlignment = BaselineAlignment.Center,
        };
    }

    private static async Task LoadImageAsync(string url, Image target)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Debug("HTML inline 图片 HTTP {Status}：{Url}", (int)resp.StatusCode, url);
                return;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;
            var bitmap = new Bitmap(ms);

            await Dispatcher.UIThread.InvokeAsync(() => target.Source = bitmap);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "HTML inline 图片加载失败：{Url}", url);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true, // Win → default browser；macOS → open；Linux → xdg-open
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "打开 URL 失败：{Url}", url);
        }
    }

    /// <summary>
    /// 防御性 URL 校验：仅允许 http(s):// 协议，拒绝 javascript:、file://、data: 等可能
    /// 的越权用法。HTML 来自爬取，理论上 DLsite 等可信源没事，但 user 也可能手动编辑
    /// description 时塞奇怪 URL，加一层 guard。
    /// </summary>
    private static bool IsSafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static IBrush GetAccentBrush()
    {
        if (Application.Current?.Resources.TryGetResource(
                "AccentFillColorDefaultBrush",
                Application.Current.ActualThemeVariant,
                out var b) == true && b is IBrush brush)
        {
            return brush;
        }
        return Brushes.SteelBlue;
    }
}
