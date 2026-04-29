namespace NineKgTools.Core.Models.Media.Source;

/// <summary>
/// 已识别但尚未入库的媒体，持久化暂存识别出的 MediaBase JSON。
/// 由后台识别任务在 AutoAddToDatabase = false 时产出，用户可在"待处理"页面手动入库。
/// </summary>
public class PendingIdentification
{
    public int Id { get; set; }

    /// <summary>关联的 MediaSource（一对一）</summary>
    public int MediaSourceId { get; set; }
    public MediaSource? MediaSource { get; set; }

    /// <summary>
    /// 识别出的 MediaBase 派生类的简单类型名（GameMedia / VideoMedia / AudioMedia / PictureMedia / TextMedia）。
    /// 用于反序列化时选对具体派生类型。
    /// </summary>
    public string MediaTypeName { get; set; } = "";

    /// <summary>MediaBase 派生类的 JSON 序列化结果</summary>
    public string MediaBaseJson { get; set; } = "";

    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
}
