using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NineKgTools.Core.Services.Configs;
public class Config
{ 
    [YamlMember(Alias = "app", Description = "Web应用设置")]
    public AppConfig App { get; set; } = null!;

    [YamlMember(Alias = "database", Description = "数据库配置")]
    public DatabaseConfig Database { get; set; } = null!;

    [YamlMember(Alias = "log", Description = "日志配置")]
    public LogConfig Log { get; set; } = null!;
    
    [YamlMember(Alias = "cache", Description = "缓存相关配置（启动前通过 yaml 固定，不在前端暴露）")]
    public CacheConfig Cache { get; set; } = null!;

    [YamlMember(Alias = "ai", Description = "AI相关配置")] 
    public AIConfig Ai { get; set; } = null!;
    
    [YamlMember(Alias = "source", Description = "媒体源相关配置")]
    public SourceConfig Source { get; set; } = null!;
    
    [YamlMember(Alias = "website", Description = "网站相关配置")]
    public WebsiteConfig Website { get; set; } = null!;
    
    [YamlMember(Alias = "files", Description = "文件相关配置")]
    public FilesConfig Files { get; set; } = null!;
    
    [YamlMember(Alias = "tasks", Description = "任务相关配置")]
    public TaskConfig Tasks { get; set; } = null!;
    
    [YamlMember(Alias = "tag_matching", Description = "标签匹配配置")]
    public TagMatchingConfig TagMatching { get; set; } = null!;
    
    [YamlMember(Alias = "search", Description = "搜索配置")]
    public SearchConfig Search { get; set; } = null!;

    [YamlMember(Alias = "identification", Description = "识别默认配置")]
    public IdentificationConfig Identification { get; set; } = null!;


    public async Task InitConfig()
    {
        // 智能查找配置文件
        var yamlFilePath = FindConfigFile();
        if (string.IsNullOrEmpty(yamlFilePath))
        {
            // 找不到 config.yaml 时尝试从 config.example.yaml 自动复制——
            // 覆盖 CI（仓库不带 config.yaml）、fresh clone、首次启动等场景。
            // Docker 容器有自己的 entrypoint cp，正常路径下走不到这里。
            yamlFilePath = TryBootstrapFromExample();
            if (string.IsNullOrEmpty(yamlFilePath))
            {
                throw new FileNotFoundException(
                    "无法找到配置文件 config.yaml，且未找到 config.example.yaml 可复制");
            }
        }
        
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        
        var yamlData = await File.ReadAllTextAsync(yamlFilePath);
        var yamlConfig = deserializer.Deserialize<Config>(yamlData);

        App = yamlConfig.App;
        Database = yamlConfig.Database ?? new DatabaseConfig();
        Log = yamlConfig.Log;
        Cache = yamlConfig.Cache ?? new CacheConfig();
        Ai = yamlConfig.Ai;
        Source = yamlConfig.Source;
        Website = yamlConfig.Website;
        Files = yamlConfig.Files;
        Tasks = yamlConfig.Tasks ?? new TaskConfig();
        TagMatching = yamlConfig.TagMatching ?? new TagMatchingConfig();
        Search = yamlConfig.Search ?? new SearchConfig();
        Identification = yamlConfig.Identification ?? new IdentificationConfig();

        CreateCache();
    }
    
    
    /// <summary>
    /// config.yaml 不存在时，从同目录下的 config.example.yaml 复制一份。
    /// 返回新创建的 config.yaml 绝对路径；example 也找不到时返回 null。
    /// </summary>
    private static string? TryBootstrapFromExample()
    {
        var examplePath = FindConfigFile("config.example.yaml");
        if (string.IsNullOrEmpty(examplePath))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(examplePath);
        if (string.IsNullOrEmpty(dir))
        {
            return null;
        }

        var targetPath = Path.Combine(dir, "config.yaml");
        try
        {
            // overwrite:false——文件已存在直接抛 IOException
            File.Copy(examplePath, targetPath, overwrite: false);
        }
        catch (IOException) when (File.Exists(targetPath))
        {
            // 并发安全：xUnit 默认 test class 间并行，多测试同时 InitConfig 时
            // File.Exists+File.Copy 之间会有竞态，输给其他线程没关系，沿用就好
        }
        return targetPath;
    }

    /// <summary>
    /// 智能查找配置文件
    /// </summary>
    public static string? FindConfigFile(string configFile = "config.yaml")
    {
        var configFileName = Path.Combine("Config", configFile);
        
        // 1. 首先检查当前工作目录
        var currentDirPath = Path.Combine(Environment.CurrentDirectory, configFileName);
        if (File.Exists(currentDirPath))
            return currentDirPath;
        
        // 2. 检查应用程序基础目录
        var baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFileName);
        if (File.Exists(baseDirPath))
            return baseDirPath;
        
        // 3. 向上查找到解决方案根目录（最多向上查找5级）
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        for (int i = 0; i < 5; i++)
        {
            if (directory == null) break;
            
            var solutionConfigPath = Path.Combine(directory.FullName, configFileName);
            if (File.Exists(solutionConfigPath))
                return solutionConfigPath;
            
            // 如果找到 .sln 文件，说明到了解决方案根目录
            if (directory.GetFiles("*.sln").Any())
            {
                var solutionRootConfigPath = Path.Combine(directory.FullName, configFileName);
                if (File.Exists(solutionRootConfigPath))
                    return solutionRootConfigPath;
                break;
            }
            
            directory = directory.Parent;
        }
        
        return null;
    }
    
    
    /// <summary>
    /// 保存配置
    /// </summary>
    public async Task SaveConfig()
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        var yamlString = serializer.Serialize(this);

        // 使用FindConfigFile找到的路径，如果找不到则使用默认路径
        var yamlFilePath = FindConfigFile() ?? Path.Combine("Config", "config.yaml");
        await File.WriteAllTextAsync(yamlFilePath, yamlString);
    }
    
    public Config Copy()
    {
        return new Config
        {
            App = App.Copy(),
            Database = Database?.Copy() ?? new DatabaseConfig(),
            Log = Log.Copy(),
            Cache = Cache?.Copy() ?? new CacheConfig(),
            Ai = Ai.Copy(),
            Source = Source.Copy(),
            Website = Website.Copy(),
            Files = Files.Copy(),
            Tasks = Tasks ?? new TaskConfig(),
            TagMatching = TagMatching ?? new TagMatchingConfig(),
            Search = Search ?? new SearchConfig(),
            Identification = Identification?.Copy() ?? new IdentificationConfig()
        };
    }

    private void CreateCache()
    {
        var cachePath = Cache.Path;

        if (Directory.Exists(cachePath))
        {
            Directory.Delete(cachePath, true); // 删除原有缓存 TODO: 在实际使用时应该注释
        }

        Directory.CreateDirectory(cachePath);
    }
}


