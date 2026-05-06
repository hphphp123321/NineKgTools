using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using NineKgTools.Core.Models.Tasks.Diagnostics;

namespace NineKgTools.Desktop.ViewModels.Components;

/// <summary>
/// 识别诊断面板 VM。包装 <see cref="IdentificationDiagnostics"/> 快照，派生 Hero 汇总、关键词、网站尝试列表。
/// </summary>
public partial class IdentificationDiagnosticsViewModel : ObservableObject
{
    public IdentificationDiagnostics? Source { get; }

    public IdentificationDiagnosticsViewModel(IdentificationDiagnostics? source)
    {
        Source = source;
        Attempts = source is null
            ? new List<WebsiteAttemptItemViewModel>()
            : source.WebsiteAttempts
                .Select((a, i) => new WebsiteAttemptItemViewModel(a, i + 1, ReferenceEquals(a, source.FinalChoice)))
                .ToList();
    }

    public bool HasDiagnostics => Source is not null;
    public bool ShowEmpty => Source is null;

    // ========== Hero ==========
    public bool IsSuccess => Source?.FinalChoice is not null;
    public bool IsFailure => Source is not null && Source.FinalChoice is null;

    public string HeroVerdict => IsSuccess ? "识别成功" : "识别失败";
    public string HeroChosenWebsite => Source?.FinalChoice?.WebsiteName ?? "";
    public string HeroChosenTitle => Source?.FinalChoice?.ResultTitle ?? "";
    public string HeroChosenScore => Source?.FinalChoice?.ResultScore is { } s ? $"得分 {s:F2}" : "";
    public string HeroFailureReason => Source?.OverallFailureReason ?? "";

    public string HeroCategory => string.IsNullOrEmpty(Source?.PossibleTopCategory)
        ? ""
        : $"分类 {Source.PossibleTopCategory}";

    public string HeroDuration
    {
        get
        {
            if (Source is null || Source.EndTime is null) return "";
            var d = Source.EndTime.Value - Source.StartTime;
            return $"耗时 {d.TotalSeconds:F2}s";
        }
    }

    public string HeroPath => Source?.SourcePath ?? "";
    public string HeroPathTruncated => TruncateMid(HeroPath, 80);

    public IBrush? HeroAccentBrush => ResolveBrush(IsSuccess
        ? "SystemFillColorSuccessBrush"
        : "SystemFillColorCriticalBrush");

    public string HeroIcon => IsSuccess ? "✓" : "⚠";

    // ========== 关键词 ==========
    public bool HasKeywords => Source?.Keywords is not null;
    public IdentificationKeywordsSnapshot? Keywords => Source?.Keywords;

    public bool HasProductCode => !string.IsNullOrEmpty(Keywords?.ProductCode);
    public bool HasCircleName => !string.IsNullOrEmpty(Keywords?.CircleName);
    public string? PrimaryKeyword => string.IsNullOrEmpty(Keywords?.PrimaryKeyword) ? "—" : Keywords?.PrimaryKeyword;
    public bool HasSecondaryKeywords => Keywords?.SecondaryKeywords.Count > 0;
    public string SecondaryKeywordsText => Keywords is null ? "" : string.Join(" · ", Keywords.SecondaryKeywords);
    public string? CleanedTitle => string.IsNullOrEmpty(Keywords?.CleanedTitle) ? "—" : Keywords?.CleanedTitle;
    public string? DetectedLanguage => Keywords?.DetectedLanguage;
    public bool HasVersion => !string.IsNullOrEmpty(Keywords?.Version);
    public bool HasDate => !string.IsNullOrEmpty(Keywords?.Date);

    // ========== 网站尝试 ==========
    public IReadOnlyList<WebsiteAttemptItemViewModel> Attempts { get; }
    public int AttemptCount => Attempts.Count;
    public string AttemptsCountText => $"网站尝试（共 {AttemptCount} 次）";
    public bool ShowEmptyAttempts => AttemptCount == 0;

    private static IBrush? ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetResource(
                key, Application.Current.ActualThemeVariant, out var obj) == true
            && obj is IBrush b)
            return b;
        return null;
    }

    private static string TruncateMid(string s, int max)
    {
        if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
        var keep = max / 2 - 2;
        return s[..keep] + " … " + s[^keep..];
    }
}

/// <summary>
/// 单次网站尝试的卡 VM。
/// </summary>
public partial class WebsiteAttemptItemViewModel : ObservableObject
{
    public WebsiteAttemptDiagnostic Source { get; }
    public int Index { get; }
    public bool IsFinal { get; }

    public WebsiteAttemptItemViewModel(WebsiteAttemptDiagnostic src, int index, bool isFinal)
    {
        Source = src;
        Index = index;
        IsFinal = isFinal;
        Candidates = src.TopCandidates
            .Select(c => new CandidateItemViewModel(c))
            .ToList();
    }

    public string IndexText => $"#{Index}";
    public string WebsiteName => Source.WebsiteName;
    public WebsiteAttemptStatus Status => Source.Status;
    public string DurationText => $"{Source.Duration.TotalMilliseconds:F0}ms";

    public string SourceText => Source.Source switch
    {
        WebsiteAttemptSource.Search => "搜索",
        WebsiteAttemptSource.ById => "ID 直查",
        WebsiteAttemptSource.Cache => "缓存",
        _ => Source.Source.ToString(),
    } + (string.IsNullOrEmpty(Source.AttemptedId) ? "" : $" · {Source.AttemptedId}");

    public string StatusText => Status switch
    {
        WebsiteAttemptStatus.Success => "命中",
        WebsiteAttemptStatus.NoMatch => "未匹配",
        WebsiteAttemptStatus.Skipped => "跳过",
        WebsiteAttemptStatus.Exception => "异常",
        WebsiteAttemptStatus.CacheHit => "缓存命中",
        _ => Status.ToString(),
    };

    public IBrush? StatusBrush => ResolveBrush(Status switch
    {
        WebsiteAttemptStatus.Success or WebsiteAttemptStatus.CacheHit => "SystemFillColorSuccessBrush",
        WebsiteAttemptStatus.NoMatch => "SystemFillColorCautionBrush",
        WebsiteAttemptStatus.Skipped => "TextFillColorTertiaryBrush",
        WebsiteAttemptStatus.Exception => "SystemFillColorCriticalBrush",
        _ => "TextFillColorPrimaryBrush",
    });

    public IBrush? StatusFillBrush => ResolveBrush(Status switch
    {
        WebsiteAttemptStatus.Success or WebsiteAttemptStatus.CacheHit => "SystemFillColorSuccessBackgroundBrush",
        WebsiteAttemptStatus.NoMatch => "SystemFillColorCautionBackgroundBrush",
        WebsiteAttemptStatus.Skipped => "LayerFillColorAltBrush",
        WebsiteAttemptStatus.Exception => "SystemFillColorCriticalBackgroundBrush",
        _ => "LayerFillColorAltBrush",
    });

    public bool IsSuccessOrCache => Status is WebsiteAttemptStatus.Success or WebsiteAttemptStatus.CacheHit;
    public bool HasReason => !IsSuccessOrCache && !string.IsNullOrEmpty(Source.Reason);
    public string? Reason => Source.Reason;
    public string? ResultTitle => Source.ResultTitle;
    public string? ResultId => Source.ResultId;
    public string ResultScoreText => Source.ResultScore is { } s ? $"得分 {s:F3}" : "";
    public bool HasResultScore => Source.ResultScore.HasValue;

    public IReadOnlyList<CandidateItemViewModel> Candidates { get; }
    public bool HasCandidates => Candidates.Count > 0;

    public string StatsText => $"扫描 {Source.TotalCandidatesScanned} · 过滤 {Source.FilteredByMinSimilarityCount} · 展示 Top {Source.TopCandidates.Count}";
    public bool ShowStats => Source.TotalCandidatesScanned > 0 || Source.FilteredByMinSimilarityCount > 0 || HasCandidates;

    private static IBrush? ResolveBrush(string key)
    {
        if (Application.Current?.Resources.TryGetResource(
                key, Application.Current.ActualThemeVariant, out var obj) == true
            && obj is IBrush b)
            return b;
        return null;
    }
}

/// <summary>
/// 单条候选的行 VM（在某次 Attempt 内）。
/// </summary>
public partial class CandidateItemViewModel : ObservableObject
{
    public CandidateDiagnostic Source { get; }

    public CandidateItemViewModel(CandidateDiagnostic src)
    {
        Source = src;
    }

    public string ScoreText => $"{Source.Score:F3}";
    public string Title => Source.Title;
    public string Id => Source.Id;
    public string? Url => Source.Url;
    public bool HasUrl => !string.IsNullOrEmpty(Source.Url);
    public string? SearchKey => Source.SearchKey;
    public bool HasSearchKey => !string.IsNullOrEmpty(Source.SearchKey);
    public bool Chosen => Source.Chosen;
}
