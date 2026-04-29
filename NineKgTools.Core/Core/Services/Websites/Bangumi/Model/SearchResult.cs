using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public class BangumiSearchResult
{
    [JsonPropertyName("results")]
    public int Results { get; set; }
    
    [JsonPropertyName("list")]
    public List<BangumiSearchResultInstance> List { get; set; }
}

public class BangumiSearchResultInstance
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; }
    
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubjectType Type { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("name_cn")]
    public string Name_Cn { get; set; }
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
    
    [JsonPropertyName("air_date")]
    public string AirDate { get; set; }     // 放送日期
    
    [JsonPropertyName("air_weekday")]
    public int AirWeekday { get; set; }  // 是周几放送
    
    [JsonPropertyName("images")]
    public BangumiImage Images { get; set; }
}