using System.Net;
using System.Net.Http.Headers;
using NineKgTools.Core.Services.Configs;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Serilog;
using LogLevel = OpenQA.Selenium.LogLevel;
using LogType = OpenQA.Selenium.LogType;

namespace NineKgTools.Core.Services.Http;

public class HttpClientOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    public string? UserAgent { get; set; }
    public ProxySettings? Proxy { get; set; }
    
    public class ProxySettings
    {
        public string? Address { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseDefaultCredentials { get; set; }
    }
}

public class HttpService
{
    private readonly HttpClientOptions _options;
    
    public HttpClient GetDefaultHttpClient() => CreateHttpClient(_options);

    public HttpService(Config config)
    {
        
        // 从配置转换为选项
        _options = new HttpClientOptions
        {
            Timeout = TimeSpan.FromSeconds(30),
            UserAgent = config.App.UserAgent,
            Proxy = config.App.Proxy.ProxyAddr != null ? new HttpClientOptions.ProxySettings
            {
                Address = config.App.Proxy.ProxyAddr,
                Username = config.App.Proxy.ProxyUser,
                Password = config.App.Proxy.ProxyPassword
            } : null
        };

    }

    private HttpClient CreateHttpClient(HttpClientOptions options)
    {
        var handler = new HttpClientHandler();
        
        if (options.Proxy?.Address != null)
        {
            var proxy = new WebProxy
            {
                Address = new Uri(options.Proxy.Address),
                UseDefaultCredentials = options.Proxy.UseDefaultCredentials
            };

            if (options.Proxy.Username != null && options.Proxy.Password != null)
            {
                proxy.Credentials = new NetworkCredential(options.Proxy.Username, options.Proxy.Password);
            }

            handler.Proxy = proxy;
            handler.UseProxy = true;
        }

        var client = new HttpClient(handler)
        {
            Timeout = options.Timeout
        };

        if (!string.IsNullOrEmpty(options.UserAgent))
        {
            client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
        }
        
        client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        
        return client;
    }

    public async Task<HtmlDocument?> Scrape(
        string url,
        HttpMethod? method = null,
        Dictionary<string, string>? headers = null,
        AuthenticationHeaderValue? authorization = null,
        string? referer = null,
        string? body = null,
        string? contentType = null,
        string? accept = null,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.Timeout);
        
        var client = ConfigureHttpClient(headers, authorization, referer);

        try
        {
            if (method == null || method == HttpMethod.Get)
            {
                return await ScrapeWithRetry(() => ScrapeGet(url, client, linkedCts.Token));
            }
            
            if (method == HttpMethod.Post)
            {
                return await ScrapeWithRetry(() => ScrapePost(url, client, body, contentType, accept, linkedCts.Token));
            }

            Log.Error("不支持的HTTP请求方法: {Method}", method);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "抓取URL失败: {Url}", url);
            return null;
        }
    }

    private async Task<T?> ScrapeWithRetry<T>(Func<Task<T?>> operation) where T : class
    {
        var retryCount = 0;
        while (retryCount < _options.MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (ex is not OperationCanceledException && retryCount < _options.MaxRetries - 1)
            {
                retryCount++;
                Log.Warning(ex, "第 {RetryCount} 次重试 (共 {MaxRetries} 次)", retryCount, _options.MaxRetries);
                await Task.Delay(_options.RetryDelay);
            }
        }
        return null;
    }

    private async Task<HtmlDocument?> ScrapeGet(string url, HttpClient client, CancellationToken cancellationToken)
    {
        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        return doc;
    }

    private async Task<HtmlDocument?> ScrapePost(
        string url, 
        HttpClient client, 
        string? body,
        string? contentType,
        string? accept,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body != null)
        {
            request.Content = new StringContent(body);
            if (contentType != null)
            {
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
        }

        if (accept != null)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        }

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        return doc;
    }

    public async Task<string?> Get(
        string url,
        Dictionary<string, string>? headers = null,
        AuthenticationHeaderValue? authorization = null,
        string? referer = null,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.Timeout);

        var client = ConfigureHttpClient(headers, authorization, referer);

        return await ScrapeWithRetry(async () =>
        {
            var response = await client.GetAsync(url, linkedCts.Token);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStringAsync(linkedCts.Token);
            return result;
        });
    }

    public async Task<byte[]?> GetBytes(
        string url,
        Dictionary<string, string>? headers = null,
        AuthenticationHeaderValue? authorization = null,
        string? referer = null,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(_options.Timeout);

        var client = ConfigureHttpClient(headers, authorization, referer);

        return await ScrapeWithRetry(async () =>
        {
            var response = await client.GetAsync(url, linkedCts.Token);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsByteArrayAsync(linkedCts.Token);
            return result;
        });
    }

    private HttpClient ConfigureHttpClient(
        Dictionary<string, string>? headers = null,
        AuthenticationHeaderValue? authorization = null,
        string? referer = null)
    {
        // 创建新的 HttpRequestMessage 而不是修改 HttpClient 的默认请求头
        var client = GetDefaultHttpClient();

        if (headers != null)
        {
            foreach (var (key, value) in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(key, value);
            }
        }

        if (authorization != null)
        {
            client.DefaultRequestHeaders.Authorization = authorization;
        }

        if (referer != null)
        {
            client.DefaultRequestHeaders.Referrer = new Uri(referer);
        }

        return client;
    }

    public ChromeDriver GetNewChromeDriver()
    {
        var options = new ChromeOptions();
        ConfigureChromeOptions(options);
        Log.Debug("创建新的ChromeDriver实例");
        return new ChromeDriver(options);
    }

    private void ConfigureChromeOptions(ChromeOptions options)
    {
        // 设置日志级别
        options.SetLoggingPreference(LogType.Browser, LogLevel.All);

        // 添加常用参数
        var arguments = new[]
        {
            "--headless",
            "--disable-gpu",
            "--no-sandbox",
            "--disable-dev-shm-usage",
            "--disable-extensions",
            "--disable-popup-blocking",
            "--enable-logging",
            "--v=1",
            "--log-level=1",
            "--disable-notifications",
            "--disable-infobars",
            "--disable-extensions-http-throttling",
            "--disable-extensions-file-access-check",
            "--disable-extensions-temp-files",
            "--disable-extensions-scheme",
            "--disable-extensions-interaction",
            "--disable-plugins-discovery",
            "--disable-plugins",
            "--disable-impl-side-painting",
            "--disable-accelerated-2d-canvas",
            "--disable-accelerated-jpeg-decoding",
            "--disable-accelerated-mjpeg-decode",
            "--disable-accelerated-video-decode",
            "--disable-accelerated-video-encode"
        };

        foreach (var argument in arguments)
        {
            options.AddArgument(argument);
        }

        // 设置代理
        if (_options.Proxy?.Address != null)
        {
            Log.Debug("为ChromeDriver配置代理服务器: {ProxyAddress}", _options.Proxy.Address);
            options.Proxy = new Proxy
            {
                HttpProxy = _options.Proxy.Address,
                SslProxy = _options.Proxy.Address
            };
        }
    }
    
}