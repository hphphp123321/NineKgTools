using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NineKgTools.Core.Models.Favorites;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Source;

namespace NineKgTools.Core.Services.Media;

/// <summary>
/// MediaBase 的多态 JSON 序列化器，用于把识别结果持久化暂存到 PendingIdentification 表。
/// 序列化前会清空 Source / RelatedMedias / Favorites 等导航属性避免循环引用和冗余。
/// 反序列化后需要由调用方重新把 Source 绑定到数据库中的 MediaSource 实体，才能送入 EF 追踪。
/// </summary>
public static class MediaBaseJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 把 MediaBase 序列化为 JSON 字符串。会临时把 Source / RelatedMedias / Favorites 置空再序列化，
    /// 序列化完成后原样恢复，对调用方而言 media 对象的可见状态不变。
    /// （不使用 media.Copy() 因为派生类未 override Copy，会丢失派生类型专有字段。）
    ///
    /// 重要：必须通过**基类** MediaBase 调用 Serialize，System.Text.Json 才会根据
    /// [JsonDerivedType] 元数据写入 "$type" 鉴别符。如果传入派生类型，STJ 认为你已经
    /// 明确指定了类型，不写鉴别符，反序列化时会退化成基类实例 —— 这正是之前的 bug。
    /// </summary>
    public static string Serialize(MediaBase media)
    {
        var originalSource = media.Source;
        var originalRelated = media.RelatedMedias;
        var originalFavorites = media.Favorites;

        try
        {
            media.Source = null;
            media.RelatedMedias = new List<MediaBase>();
            media.Favorites = new List<Favorite>();

            // 关键：用 typeof(MediaBase) 触发多态序列化，写入 $type 鉴别符。
            // STJ 会自动根据 media 的真实运行时类型（GameMedia / VideoMedia 等）
            // 写入对应的派生类型字段。
            return JsonSerializer.Serialize(media, typeof(MediaBase), Options);
        }
        finally
        {
            media.Source = originalSource;
            media.RelatedMedias = originalRelated;
            media.Favorites = originalFavorites;
        }
    }

    /// <summary>
    /// 反序列化 JSON 到 MediaBase 派生类。调用方需要随后手动把 source 绑到 result.Source 上。
    /// </summary>
    public static MediaBase? Deserialize(string json, MediaSource? source = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        var result = JsonSerializer.Deserialize<MediaBase>(json, Options);
        if (result != null && source != null)
            result.Source = source;

        return result;
    }
}
