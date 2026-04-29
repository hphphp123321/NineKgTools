using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace NineKgTools.Core.Models.Tasks.Diagnostics;

/// <summary>
/// 单次识别任务（SingleSourceIdentificationTask）执行过程中收集的"诊断快照"。
/// 包括关键词解析、各网站的尝试与候选、最终决策，便于前端展示"识别为什么命中/为什么失败"。
/// 仅在内存中流转，最终随 TaskExecutionInfo / TaskProgress 暴露给前端，不进数据库。
/// </summary>
public class IdentificationDiagnostics
{
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// 媒体源推断的顶层分类（Audio/Video/Game/Text/Picture/Unknown）。
    /// 用字符串而非 enum，避免反序列化耦合（前端只关心展示）。
    /// </summary>
    public string PossibleTopCategory { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 关键词提取快照（首个执行 SearchMediaAsync 的网站填入即可，后续不覆盖）。
    /// </summary>
    public IdentificationKeywordsSnapshot? Keywords { get; set; }

    /// <summary>
    /// 按调用顺序记录的网站尝试。
    /// 注意：必须保留 setter，否则 System.Text.Json 反序列化会跳过这个属性，前端永远看到空数组。
    /// </summary>
    public List<WebsiteAttemptDiagnostic> WebsiteAttempts { get; set; } = new();

    /// <summary>
    /// 最终命中的网站 Attempt：返回 <see cref="WebsiteAttempts"/> 里第一条
    /// Status 为 <see cref="WebsiteAttemptStatus.Success"/> 或 <see cref="WebsiteAttemptStatus.CacheHit"/> 的记录；
    /// 全部失败时返回 null。
    /// 计算属性而非引用字段——避免 JSON 序列化时变成独立拷贝、前端 ReferenceEquals 永远 false。
    /// 序列化输出仅供前端调试参考，前端实际渲染请直接遍历 <see cref="WebsiteAttempts"/>。
    /// </summary>
    [JsonIgnore]
    public WebsiteAttemptDiagnostic? FinalChoice =>
        WebsiteAttempts.FirstOrDefault(a =>
            a.Status == WebsiteAttemptStatus.Success || a.Status == WebsiteAttemptStatus.CacheHit);

    /// <summary>
    /// 整体失败原因（如"所有网站均未匹配"/"用户取消"/"未找到或未启用指定网站"）。
    /// 成功时为 null。
    /// </summary>
    public string? OverallFailureReason { get; set; }

    /// <summary>
    /// 当前正在进行的 Attempt（仅供同进程内的上报方法使用，不参与序列化）。
    /// </summary>
    [JsonIgnore]
    public WebsiteAttemptDiagnostic? CurrentAttempt { get; set; }

    /// <summary>
    /// 关联的进度报告器（仅供同进程内 Search 类等"深处代码"通过
    /// <see cref="NineKgTools.Core.Services.Tasks.Diagnostics.IdentificationDiagnosticsContext"/>
    /// 直接写任务日志，无需修改方法签名传 IProgressReporter）。
    /// </summary>
    [JsonIgnore]
    public NineKgTools.Core.Services.Tasks.Interfaces.IProgressReporter? Reporter { get; set; }
}
