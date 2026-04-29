using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class SourceConfig
{
    [YamlMember(Alias = "watch_folders", Description = "监控文件夹")]
    public List<string> WatchFolders { get; set; } = new();
    
    
    public SourceConfig Copy()
    {
        return new SourceConfig
        {
            WatchFolders = WatchFolders.ToList()
        };
    }
}