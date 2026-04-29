using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public class SubjectInfo
{
    [JsonPropertyName("date")]
    public string Date { get; set; }
    
    [JsonPropertyName("platform")]
    public string Platform { get; set; }
    
    [JsonPropertyName("images")]
    public BangumiImage Images { get; set; }
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("name_cn")]
    public string Name_Cn { get; set; }
    
    [JsonPropertyName("tags")]
    public List<BangumiTag> Tags { get; set; }
    
    [JsonPropertyName("infobox")]
    [JsonConverter(typeof(InfoBoxJsonConverter))]
    public Dictionary<string, object> Infobox { get; set; } = [];
    
    [JsonPropertyName("rating")]
    public Rating Rating { get; set; }
    
    [JsonPropertyName("total_episodes")]
    public int Episodes { get; set; }
    
    [JsonPropertyName("collection")]
    public Collection Collection { get; set; }
    
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("eps")]
    public int Eps { get; set; }
    
    [JsonPropertyName("volumes")]
    public int Volumes { get; set; }
    
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }
    
    [JsonPropertyName("nsfw")]
    public bool Nsfw { get; set; }
    
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubjectType Type { get; set; }
    
    [JsonPropertyName("meta_tags")]
    public List<string> MetaTags { get; set; }
}

public class BangumiTag
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("count")]
    public int Count { get; set; }
}


public class Rating
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("count")]
    public Dictionary<string, int> Count { get; set; } // 使用字典来表示评分的计数
    
    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public class Collection
{
    [JsonPropertyName("on_hold")]
    public int OnHold { get; set; }
    
    [JsonPropertyName("dropped")]
    public int Dropped { get; set; }
    
    [JsonPropertyName("wish")]
    public int Wish { get; set; }
    
    [JsonPropertyName("collect")]
    public int Collect { get; set; }
    
    [JsonPropertyName("doing")]
    public int Doing { get; set; }
}