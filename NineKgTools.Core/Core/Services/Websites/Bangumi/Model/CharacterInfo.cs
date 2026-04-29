using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

public class CharacterInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; }
    
    [JsonPropertyName("images")]
    public BangumiImage Images { get; set; }
    
    [JsonPropertyName("relation")]
    public string Relation { get; set; }
    
    [JsonPropertyName("actors")]
    public List<ActorInfo> Actors { get; set; }
}

public class ActorInfo
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
    
    [JsonPropertyName("short_summary")]
    public string ShortSummary { get; set; }
    
    [JsonPropertyName("locked")]
    public bool Locked { get; set; }
}

public enum CharaterType
{
    // 角色，机体，舰船，组织...
    Character = 1,
    Mecha,
    Ship,
    Organization
}