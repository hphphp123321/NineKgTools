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
    /// 15s 超时（DLsite 图片 CDN 通常 1-2s 出来；放宽到 15s 兼容慢网络）。
    /// 配 SocketsHttpHandler 显式 enable AutomaticDecompression（部分 CDN 返回 gzip）。
    /// </summary>
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            // 跟随 redirects（部分 CDN 会 302）
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        // 默认 UA：DLsite 等部分 CDN 对没 UA 的请求返回 403
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 NineKgTools/1.0");
        return client;
    }

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
                            var rawHref = node.GetAttributeValue("href", "")?.Trim();
                            var href = NormalizeUrl(rawHref);
                            var text = HtmlEntity.DeEntitize(node.InnerText) ?? "";
                            if (!string.IsNullOrEmpty(href) && IsSafeUrl(href))
                                inlines.Add(MakeHyperlink(text, href));
                            else
                                ProcessNodes(node.ChildNodes, inlines, weight, style, underline: true);
                            break;
                        }

                        case "img":
                        {
                            var rawSrc = node.GetAttributeValue("src", "")?.Trim();
                            var normalizedSrc = NormalizeUrl(rawSrc);
                            if (!string.IsNullOrEmpty(normalizedSrc) && IsSafeUrl(normalizedSrc))
                            {
                                // 图片前后各加 LineBreak 让它独占一行——inline 巨图夹在文字里很丑。
                                // 末尾若已有 LineBreak 就不再加，避免双倍空行。
                                if (inlines.Count > 0 && inlines[^1] is not LineBreak)
                                    inlines.Add(new LineBreak());
                                var alt = node.GetAttributeValue("alt", "");
                                inlines.Add(MakeImage(normalizedSrc, alt));
                                inlines.Add(new LineBreak());
                            }
                            else
                            {
                                Log.Debug("HTML inline 图片 URL 非法或不安全，跳过：{Src}", rawSrc);
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
    /// &lt;img src="..."&gt; → InlineUIContainer + StackPanel(Image + 占位 TextBlock)。
    /// HttpClient 后台异步加载；加载中显示"图片加载中..."占位，加载完成隐藏占位 + set Source；
    /// 失败显示"图片加载失败"+ HTTP code（用户能看到 hint，详细 ex 走 Log.Information）。
    /// </summary>
    private static Inline MakeImage(string src, string alt)
    {
        var image = new Image
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            MaxWidth = MaxImageWidth,
            MaxHeight = MaxImageHeight,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (!string.IsNullOrEmpty(alt))
            ToolTip.SetTip(image, alt);

        var placeholder = new TextBlock
        {
            Text = "🖼 图片加载中…",
            FontSize = 11,
            Opacity = 0.55,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var stack = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        stack.Children.Add(image);
        stack.Children.Add(placeholder);

        _ = LoadImageAsync(src, image, placeholder);

        return new InlineUIContainer
        {
            Child = stack,
            BaselineAlignment = BaselineAlignment.Center,
        };
    }

    private static async Task LoadImageAsync(string url, Image target, TextBlock placeholder)
    {
        try
        {
            // 部分 CDN（含 DLsite img.dlsite.jp）对无 Referer 的请求返回 403。
            // 用 image URL 自身的 origin 作 Referer 通常能通过 hotlink 检查。
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            try
            {
                var uri = new Uri(url);
                req.Headers.Referrer = new Uri($"{uri.Scheme}://{uri.Host}/");
            }
            catch { /* URL 解析失败不阻塞请求 */ }

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            if (!resp.IsSuccessStatusCode)
            {
                Log.Information("HTML inline 图片 HTTP {Status}：{Url}", (int)resp.StatusCode, url);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    placeholder.Text = $"🖼 图片加载失败 (HTTP {(int)resp.StatusCode})";
                });
                return;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            Bitmap bitmap;
            try { bitmap = new Bitmap(ms); }
            catch (Exception decodeEx)
            {
                Log.Information(decodeEx, "HTML inline 图片解码失败：{Url}", url);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    placeholder.Text = "🖼 图片解码失败";
                });
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                target.Source = bitmap;
                placeholder.IsVisible = false;
                // 触发 Image 的 measure pass——Inline 流中的 InlineUIContainer 在 child
                // size 变化时不一定自动 invalidate，显式调一下保险
                target.InvalidateMeasure();
                if (target.Parent is Layoutable parent) parent.InvalidateMeasure();
            });
            Log.Debug("HTML inline 图片加载成功：{Url} ({W}x{H})", url, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch (Exception ex)
        {
            Log.Information(ex, "HTML inline 图片加载异常：{Url}", url);
            try
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    placeholder.Text = "🖼 图片加载失败";
                });
            }
            catch { /* UI thread 不可用时静默 */ }
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

    /// <summary>
    /// 把 HTML 里常见的相对 / 协议无关 URL 转成绝对 https URL。
    /// 主要场景：DLsite 描述里很多图片用 protocol-relative <c>//img.dlsite.jp/...</c>，
    /// 浏览器自动补协议但 .NET HttpClient 不会，会被 IsSafeUrl 拒绝。
    /// </summary>
    private static string NormalizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        url = url.Trim();
        // protocol-relative: //img.dlsite.jp/foo.jpg → https://img.dlsite.jp/foo.jpg
        if (url.StartsWith("//", StringComparison.Ordinal)) return "https:" + url;
        return url;
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
