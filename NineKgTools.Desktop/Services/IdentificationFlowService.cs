using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NineKgTools.Core.Models.Identification;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;
using NineKgTools.Core.Models.Tasks.Diagnostics;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Files;
using NineKgTools.Core.Services.Tasks.Diagnostics;
using NineKgTools.Core.Services.Tasks.Progress;
using NineKgTools.Core.Services.Websites;
using NineKgTools.Desktop.Views.Dialogs;
using Serilog;

namespace NineKgTools.Desktop.Services;

/// <summary>
/// 桌面端"交互式识别"共享流程：选项对话框（A）→ 进度+诊断对话框（B）→ 预览/入库对话框（C）。
///
/// 与 Web 端 <c>UnknownPage.HandleIdentifyMediaAsync</c> / <c>SourceDetailPage.HandleReidentifyAsync</c>
/// 行为对齐：调 <see cref="FilesService.GetMediaByPath"/> 同步等结果（不是 fire-and-forget Hangfire 队列）；
/// 识别期间通过 <see cref="IdentificationDiagnosticsContext"/> 自开作用域采集诊断，实时反馈到 B 对话框。
///
/// 调用方：
/// - <c>MediaDetailViewModel.ReidentifyAsync</c>（媒体详情窗右栏"重新识别"）
/// - <c>PendingMediaViewModel.IdentifyAsync</c>（待识别 Tab"识别"行操作）
/// - <c>PendingMediaViewModel.ReidentifyAsync</c>（待入库 Tab"重新识别"行操作）
/// </summary>
public class IdentificationFlowService
{
    private readonly Config _config;
    private readonly FilesService _filesService;
    private readonly WebsiteService _websiteService;

    public IdentificationFlowService(Config config, FilesService filesService, WebsiteService websiteService)
    {
        _config = config;
        _filesService = filesService;
        _websiteService = websiteService;
    }

    /// <summary>
    /// 跑一次完整流程。返回 <see cref="IdentificationFlowResult"/> 让调用方按结果刷新 UI。
    /// 错误均已 Log，UI 侧脱敏文案由调用方决定（通常 Serilog 已足够）。
    /// </summary>
    public async Task<IdentificationFlowResult> RunInteractiveAsync(string path, IdentificationFlowKind kind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Log.Warning("IdentificationFlowService: 路径为空，跳过");
            return IdentificationFlowResult.Canceled;
        }

        // ============= 步骤 1：选项对话框 =============
        var initOpts = _config.Identification.ToIdentificationOptions();
        initOpts.AutoAddToDatabase = false;       // 手动 / 重识别一律手动确认入库
        initOpts.SourcePath = path;
        if (kind == IdentificationFlowKind.Reidentify)
        {
            initOpts.SkipCache = true;            // 重识别强制跳缓存（与 Web SourceDetailPage 一致）
        }

        var availableWebsites = _websiteService.WebsiteNameMap.Keys.ToList();

        IdentificationOptions? options;
        try
        {
            options = await IdentificationOptionsDialog.ShowAsync(
                path,
                initOpts,
                availableWebsites,
                isReidentify: kind == IdentificationFlowKind.Reidentify);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "识别选项对话框异常：{Path}", path);
            return IdentificationFlowResult.Failed;
        }

        if (options is null)
        {
            Log.Debug("用户取消识别选项：{Path}", path);
            return IdentificationFlowResult.Canceled;
        }

        // ============= 步骤 2：进度 + 诊断对话框，并跑识别 =============
        var diagnostics = new IdentificationDiagnostics
        {
            SourcePath = path,
            PossibleTopCategory = TryGetPossibleTopCategory(path),
            StartTime = DateTime.UtcNow,
        };

        var reporter = new DialogProgressReporter();
        diagnostics.Reporter = reporter; // 让深处 Search 类能写日志（即使桌面端非 SingleSourceIdentificationTask）

        using var cts = new CancellationTokenSource();
        var handle = IdentificationProgressDialog.Show(reporter, diagnostics, onCancelRequested: () =>
        {
            try { cts.Cancel(); }
            catch (Exception ex) { Log.Warning(ex, "取消识别请求异常：{Path}", path); }
        });

        MediaBase? media = null;
        Exception? identifyError = null;
        var wasCanceled = false;

        // 开诊断作用域：从此 IWebsite / Search 实现里的 RecordKeywords / RecordCandidates / MarkChosen
        // 都会写到 diagnostics 上，B 对话框通过 100ms timer 拉取渲染
        using (IdentificationDiagnosticsContext.BeginScope(diagnostics))
        {
            try
            {
                media = await _filesService.GetMediaByPath(path, options, reporter, cts.Token);
            }
            catch (OperationCanceledException)
            {
                wasCanceled = true;
            }
            catch (Exception ex)
            {
                identifyError = ex;
                Log.Error(ex, "桌面端交互式识别失败：{Path}", path);
            }
        }

        diagnostics.EndTime = DateTime.UtcNow;
        if (media == null && diagnostics.OverallFailureReason == null)
        {
            diagnostics.OverallFailureReason = wasCanceled
                ? "用户取消"
                : (identifyError != null ? "识别异常" : "未能匹配任何网站");
        }

        await handle.CloseAsync();

        if (wasCanceled) return IdentificationFlowResult.Canceled;
        if (identifyError != null) return IdentificationFlowResult.Failed;
        if (media == null)
        {
            Log.Information("识别未能匹配：{Path}", path);
            return IdentificationFlowResult.NoMatch;
        }

        // ============= 步骤 3：预览 + 用户确认入库 =============
        bool approve;
        try
        {
            approve = await PendingMediaPreviewDialog.ShowAsync(media, media.Source);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "识别结果预览对话框异常：{Path}", path);
            return IdentificationFlowResult.Failed;
        }

        if (!approve)
        {
            // 用户在预览界面取消——与 Web 行为一致：识别结果丢弃，不保留 Pending（Web 也是这样）
            Log.Information("用户取消入库：{Path}", path);
            return IdentificationFlowResult.UserDeclined;
        }

        try
        {
            await _filesService.AddMediaToDatabase(media);
            Log.Information("交互式识别入库成功：{Title} ({Path})", media.Title, path);
            return IdentificationFlowResult.Imported;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "交互式识别入库失败：{Path}", path);
            return IdentificationFlowResult.Failed;
        }
    }

    private static string TryGetPossibleTopCategory(string path)
    {
        try
        {
            var src = MediaSourceFactory.Create(path);
            return src.PossibleTopCategory.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }
}

public enum IdentificationFlowKind
{
    /// <summary>首次手动识别（待识别 Tab）。SkipCache 不强制；标题"手动识别选项"。</summary>
    FirstTime,

    /// <summary>重新识别（详情页 / 待入库 Tab）。SkipCache=true；标题"重新识别选项"。</summary>
    Reidentify,
}

public enum IdentificationFlowResult
{
    /// <summary>用户在选项 / 进度阶段点了取消</summary>
    Canceled,

    /// <summary>识别完成但没有任何网站匹配</summary>
    NoMatch,

    /// <summary>识别 / 入库过程抛出异常（详情已 Log）</summary>
    Failed,

    /// <summary>识别成功但用户在预览阶段拒绝入库</summary>
    UserDeclined,

    /// <summary>识别成功且用户确认入库——调用方应刷新列表</summary>
    Imported,
}
