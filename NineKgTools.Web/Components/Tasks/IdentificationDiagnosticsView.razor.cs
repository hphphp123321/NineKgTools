using Microsoft.AspNetCore.Components;
using MudBlazor;
using NineKgTools.Core.Models.Tasks.Diagnostics;

namespace NineKgTools.Components.Tasks;

public partial class IdentificationDiagnosticsView
{
    [Parameter] public IdentificationDiagnostics? Diagnostics { get; set; }

    private static string TruncateMid(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        var keep = (max - 3) / 2;
        return $"{s[..keep]}...{s[^keep..]}";
    }

    private string HeroAccentClass() => Diagnostics?.FinalChoice != null
        ? "diag-hero--success"
        : "diag-hero--fail";

    private string HeroIcon() => Diagnostics?.FinalChoice != null
        ? Icons.Material.Filled.Insights
        : Icons.Material.Filled.SearchOff;

    private Color HeroColor() => Diagnostics?.FinalChoice != null
        ? Color.Success
        : Color.Warning;

    private static string StatusText(WebsiteAttemptStatus s) => s switch
    {
        WebsiteAttemptStatus.Success => "成功",
        WebsiteAttemptStatus.NoMatch => "未匹配",
        WebsiteAttemptStatus.Skipped => "跳过",
        WebsiteAttemptStatus.Exception => "异常",
        WebsiteAttemptStatus.CacheHit => "缓存命中",
        _ => s.ToString(),
    };

    private static Color StatusColor(WebsiteAttemptStatus s) => s switch
    {
        WebsiteAttemptStatus.Success => Color.Success,
        WebsiteAttemptStatus.CacheHit => Color.Info,
        WebsiteAttemptStatus.NoMatch => Color.Default,
        WebsiteAttemptStatus.Skipped => Color.Default,
        WebsiteAttemptStatus.Exception => Color.Error,
        _ => Color.Default,
    };

    private static string StatusClass(WebsiteAttemptStatus s) => s switch
    {
        WebsiteAttemptStatus.Success => "success",
        WebsiteAttemptStatus.CacheHit => "cache",
        WebsiteAttemptStatus.NoMatch => "nomatch",
        WebsiteAttemptStatus.Skipped => "skipped",
        WebsiteAttemptStatus.Exception => "exception",
        _ => "default",
    };

    private static string SourceText(WebsiteAttemptSource src) => src switch
    {
        WebsiteAttemptSource.ById => "ID 直查",
        WebsiteAttemptSource.Search => "搜索",
        WebsiteAttemptSource.Cache => "缓存",
        _ => src.ToString(),
    };
}
