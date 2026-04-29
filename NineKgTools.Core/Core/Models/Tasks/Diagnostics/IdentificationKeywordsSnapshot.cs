using System.Collections.Generic;

namespace NineKgTools.Core.Models.Tasks.Diagnostics;

/// <summary>
/// <see cref="NineKgTools.Utils.MediaKeywords"/> 的扁平快照，仅保留前端展示所需字段。
/// </summary>
public class IdentificationKeywordsSnapshot
{
    public string PrimaryKeyword { get; set; } = string.Empty;
    public List<string> SecondaryKeywords { get; set; } = new();
    public string? ProductCode { get; set; }
    public string? CircleName { get; set; }
    public string CleanedTitle { get; set; } = string.Empty;
    public string DetectedLanguage { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Date { get; set; }
}
