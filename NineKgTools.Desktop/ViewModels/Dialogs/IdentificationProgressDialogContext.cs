using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NineKgTools.Core.Models.Tasks;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Progress;

namespace NineKgTools.Desktop.ViewModels.Dialogs;

/// <summary>
/// 识别进度对话框 VM。把 <see cref="DialogProgressReporter.OnProgress"/> 的快速事件流
/// + 实时累积的 <see cref="IdentificationDiagnostics"/> 通过 100ms DispatcherTimer 节流
/// 刷新到 UI，避免 Avalonia 渲染管线过载。
///
/// 线程模型：OnProgress 事件可能从 IO 线程触发；HandleProgress 内部把 entry 写到 _pendingEntry
/// 字段（无锁，赋值原子），由 UI 线程上的 DispatcherTimer Tick 统一拉取并应用——同时也借此 Tick
/// 把 diagnostics.WebsiteAttempts 增量同步到 ObservableCollection。
/// </summary>
public partial class IdentificationProgressDialogContext : ObservableObject, System.IDisposable
{
    public IdentificationDiagnostics? Diagnostics { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressPercentText))]
    private double _progress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrentItem))]
    private string? _currentItem;

    [ObservableProperty] private TaskLogLevel _level = TaskLogLevel.Info;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private bool _isCancelling;

    /// <summary>识别仍在进行；完成 / 失败 / 取消后置 false（CancelCommand CanExecute 用）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private bool _isFinalised;

    public bool IsRunning => !IsCancelling && !IsFinalised;
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);
    public bool HasCurrentItem => !string.IsNullOrWhiteSpace(CurrentItem);
    public string ProgressPercentText => $"{Progress:F1}%";

    public ObservableCollection<WebsiteAttemptItemVm> AttemptItems { get; } = new();
    public bool HasAttempts => AttemptItems.Count > 0;
    public bool HasKeywords => Diagnostics?.Keywords != null;

    public string? KeywordsPrimary => Diagnostics?.Keywords?.PrimaryKeyword;
    public string? KeywordsProductCode => Diagnostics?.Keywords?.ProductCode;
    public string? KeywordsCircleName => Diagnostics?.Keywords?.CircleName;
    public string? KeywordsCleanedTitle => Diagnostics?.Keywords?.CleanedTitle;

    public event System.Action? CancelRequested;

    private TaskLogEntry? _pendingEntry;
    private readonly DispatcherTimer _timer;
    private int _lastAttemptCount;
    private bool _lastKeywordsKnown;

    public IdentificationProgressDialogContext(IdentificationDiagnostics? diagnostics)
    {
        Diagnostics = diagnostics;
        _timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
        _timer.Start();
    }

    /// <summary>外部（IdentificationFlowService）订阅 reporter.OnProgress 后转发到这里。无线程切换。</summary>
    public void HandleProgress(TaskLogEntry entry)
    {
        _pendingEntry = entry; // 单写者覆盖式快照——丢中间帧是可接受的（节流就是这个意图）
    }

    private void OnTimerTick(object? sender, System.EventArgs e)
    {
        // 1) 把最近一次 reporter 事件刷到 UI
        var entry = _pendingEntry;
        if (entry != null)
        {
            _pendingEntry = null;
            // 仅在 entry 真的带了进度值时刷新（避免 Debug 级别覆盖到 100% 后退）
            // TaskLogEntry.Progress 是 double?，Debug 级别可能为 null —— null 不更新进度
            if (entry.Progress is double p && (p > 0 || Progress == 0))
                Progress = p;
            Message = entry.Message;
            CurrentItem = entry.CurrentItem;
            Level = entry.Level;
        }

        // 2) 同步诊断条
        SyncDiagnostics();
    }

    private void SyncDiagnostics()
    {
        if (Diagnostics is null) return;

        // Keywords：只首次出现时通知
        if (!_lastKeywordsKnown && Diagnostics.Keywords != null)
        {
            _lastKeywordsKnown = true;
            OnPropertyChanged(nameof(HasKeywords));
            OnPropertyChanged(nameof(KeywordsPrimary));
            OnPropertyChanged(nameof(KeywordsProductCode));
            OnPropertyChanged(nameof(KeywordsCircleName));
            OnPropertyChanged(nameof(KeywordsCleanedTitle));
        }

        // WebsiteAttempts：增量+原地刷新最后一条（活跃 attempt）
        var src = Diagnostics.WebsiteAttempts;
        var srcCount = src.Count;

        // 新增的尾部 attempts
        for (var i = _lastAttemptCount; i < srcCount; i++)
        {
            AttemptItems.Add(new WebsiteAttemptItemVm(src[i]));
        }

        // 活跃 / 最近一条 attempt 状态可能更新（NoMatch → Success / TopCandidates 填充等）—— 整段 refresh
        // 简单策略：只刷新最后一项（活跃中的）+ 倒数第二项（可能刚被定型）
        for (var i = System.Math.Max(0, srcCount - 2); i < srcCount; i++)
        {
            if (i < AttemptItems.Count)
                AttemptItems[i].Refresh(src[i]);
        }

        if (_lastAttemptCount != srcCount)
        {
            _lastAttemptCount = srcCount;
            OnPropertyChanged(nameof(HasAttempts));
        }
    }

    /// <summary>识别结束 / 失败 / 取消后调用：停 timer，让 dialog 显示最终态。</summary>
    public void Finalise()
    {
        _timer.Stop();
        // 末次同步，确保最后一条 attempt 状态正确
        SyncDiagnostics();
        var entry = _pendingEntry;
        if (entry != null)
        {
            _pendingEntry = null;
            Message = entry.Message;
            CurrentItem = entry.CurrentItem;
            Level = entry.Level;
            if (entry.Progress is double p && p > 0) Progress = p;
        }
        IsFinalised = true;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        if (IsCancelling || IsFinalised) return;
        IsCancelling = true;
        Message = "正在取消...";
        CancelRequested?.Invoke();
    }

    private bool CanCancel() => !IsCancelling && !IsFinalised;

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }
}

/// <summary>诊断条里单个网站 attempt 的 VM。Refresh 用于增量更新（活跃 attempt 的状态变化）。</summary>
public partial class WebsiteAttemptItemVm : ObservableObject
{
    [ObservableProperty] private string _websiteName = string.Empty;
    [ObservableProperty] private WebsiteAttemptStatus _status;
    [ObservableProperty] private double _durationMs;
    [ObservableProperty] private int _candidateCount;
    [ObservableProperty] private string? _reason;
    [ObservableProperty] private string? _chosenTitle;

    public WebsiteAttemptItemVm(WebsiteAttemptDiagnostic src) => Refresh(src);

    public void Refresh(WebsiteAttemptDiagnostic src)
    {
        WebsiteName = src.WebsiteName;
        Status = src.Status;
        DurationMs = src.Duration.TotalMilliseconds;
        CandidateCount = src.TopCandidates.Count;
        Reason = src.Reason;
        ChosenTitle = src.TopCandidates.FirstOrDefault(c => c.Chosen)?.Title
                      ?? (src.Status == WebsiteAttemptStatus.Success || src.Status == WebsiteAttemptStatus.CacheHit
                          ? src.ResultTitle
                          : null);
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrushKey));
        OnPropertyChanged(nameof(HasChosenTitle));
        OnPropertyChanged(nameof(HasReason));
        OnPropertyChanged(nameof(DurationDisplay));
    }

    public bool HasChosenTitle => !string.IsNullOrEmpty(ChosenTitle);
    public bool HasReason => !string.IsNullOrEmpty(Reason) && !HasChosenTitle;
    public string DurationDisplay => $"{DurationMs:F0}ms";

    public string StatusIcon => Status switch
    {
        WebsiteAttemptStatus.Success => "✓",
        WebsiteAttemptStatus.CacheHit => "⚡",
        WebsiteAttemptStatus.NoMatch => "—",
        WebsiteAttemptStatus.Skipped => "⊘",
        WebsiteAttemptStatus.Exception => "⚠",
        _ => "·"
    };

    public string StatusText => Status switch
    {
        WebsiteAttemptStatus.Success => "命中",
        WebsiteAttemptStatus.CacheHit => "缓存",
        WebsiteAttemptStatus.NoMatch => "未匹配",
        WebsiteAttemptStatus.Skipped => "跳过",
        WebsiteAttemptStatus.Exception => "异常",
        _ => "进行中"
    };

    /// <summary>FluentAvalonia 系统语义色，AXAML 用 DynamicResource 索引。</summary>
    public string StatusBrushKey => Status switch
    {
        WebsiteAttemptStatus.Success => "SystemFillColorSuccessBrush",
        WebsiteAttemptStatus.CacheHit => "SystemFillColorAttentionBrush",
        WebsiteAttemptStatus.NoMatch => "SystemFillColorNeutralBrush",
        WebsiteAttemptStatus.Skipped => "SystemFillColorNeutralBrush",
        WebsiteAttemptStatus.Exception => "SystemFillColorCriticalBrush",
        _ => "SystemFillColorAttentionBrush"
    };
}
