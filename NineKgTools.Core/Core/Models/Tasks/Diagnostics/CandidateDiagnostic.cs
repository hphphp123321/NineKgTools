namespace NineKgTools.Core.Models.Tasks.Diagnostics;

/// <summary>
/// 一条候选（搜索返回项）的诊断快照。
/// </summary>
public class CandidateDiagnostic
{
    /// <summary>网站特定 ID（DLsite 的 RJ 号 / Bangumi 的整数 / Steam 的 AppID）。</summary>
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Url { get; set; }

    /// <summary>相关性得分（0-1，1 表示完美匹配）。</summary>
    public double Score { get; set; }

    /// <summary>实际命中该候选的搜索查询（多查询策略下哪一组关键词命中的）。</summary>
    public string? SearchKey { get; set; }

    /// <summary>该候选是否被最终采用（前端高亮用）。</summary>
    public bool Chosen { get; set; }
}
