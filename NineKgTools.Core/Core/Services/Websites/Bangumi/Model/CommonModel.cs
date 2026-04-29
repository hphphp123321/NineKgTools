using System.Text.Json.Serialization;

namespace NineKgTools.Core.Services.Websites.Bangumi.Model;

// 1 为 书籍
// 2 为 动画
// 3 为 音乐
// 4 为 游戏
// 6 为 三次元
public enum SubjectType 
{
    Book = 1,
    Animation,
    Music,
    Game,
    Real = 6
}

public class BangumiImage
{
    [JsonPropertyName("small")]
    public string Small { get; set; }
    
    [JsonPropertyName("grid")]
    public string Grid { get; set; }
    
    [JsonPropertyName("large")]
    public string Large { get; set; }
    
    [JsonPropertyName("medium")]
    public string Medium { get; set; }
    
    [JsonPropertyName("common")]
    public string Common { get; set; }
}