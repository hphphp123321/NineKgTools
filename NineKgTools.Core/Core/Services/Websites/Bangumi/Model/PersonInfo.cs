using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public enum PersonType
{
    Individual = 1,
    Company,
    Combination
}

public enum BloodType
{
    A = 1,
    B,
    AB,
    O
}

public enum PersonCareer
{
    Producer = 1,   // 制作人
    Mangaka,        // 漫画家
    Artist,         // 画师
    Seiyu,          // 声优
    Writer,         // 剧本
    Illustrator,    // 插画
    Actor           // 演员
}

public class PersonInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PersonType Type { get; set; }
    
    [JsonPropertyName("career")]
    [JsonConverter(typeof(PersonCareerConverter))]
    public List<PersonCareer> Career { get; set; }
    
    [JsonPropertyName("images")]
    public BangumiImage Images { get; set; }
    
    [JsonPropertyName("relation")]
    public string Relation { get; set; } // 在作品中担任的职责
    
    [JsonPropertyName("eps")]
    public string Eps { get; set; }
}

public class PersonDetail : PersonInfo
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
    
    [JsonPropertyName("infobox")]
    [JsonConverter(typeof(InfoBoxJsonConverter))]
    public Dictionary<string, object> Infobox { get; set; } = [];
    
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    
    // 忽略null值
    [JsonPropertyName("blood_type")]
    public BloodType? BloodType { get; set; }
    
    [JsonPropertyName("birth_year")]
    public int? BirthYear { get; set; }
    
    [JsonPropertyName("birth_month")]
    public int? BirthMonth { get; set; }
    
    [JsonPropertyName("birth_day")]
    public int? BirthDay { get; set; }
}