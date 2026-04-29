using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class FilesConfig
{

    [YamlMember(Alias = "minimum_file_size", Description = "最小文件大小（字节），小于此大小的文件将被忽略")]
    public long MinimumFileSize { get; set; } = 1024; // 默认1KB
    
    [YamlMember(Alias = "ignored_files", Description = "忽略的文件名列表（精确匹配，不区分大小写）")]
    public List<string> IgnoredFiles { get; set; } = new()
    {
        "Thumbs.db",
        ".DS_Store",
        "desktop.ini",
        ".gitkeep",
        ".gitignore"
    };
    
    [YamlMember(Alias = "ignored_patterns", Description = "忽略的文件名模式（支持简单通配符：* 匹配任意字符）")]
    public List<string> IgnoredPatterns { get; set; } = new()
    {
        ".*",      // 所有以点开头的文件
        "~*",      // 所有以波浪号开头的文件（临时文件）
        "*.tmp",   // 所有.tmp扩展名的文件
        "*.temp",  // 所有.temp扩展名的文件
        "*.cache", // 所有.cache扩展名的文件
        "*.log",   // 所有.log扩展名的文件
        "*.bak",   // 所有.bak扩展名的文件
        "*.swp"    // 所有.swp扩展名的文件（vim临时文件）
    };

    [YamlMember(Alias = "skip_hidden_files", Description = "是否跳过隐藏文件")]
    public bool SkipHiddenFiles { get; set; } = true;

    [YamlMember(Alias = "skip_system_files", Description = "是否跳过系统文件")]
    public bool SkipSystemFiles { get; set; } = true;

    [YamlMember(Alias = "allowed_extensions", Description = "允许的文件扩展名列表（为空表示允许所有扩展名）")]
    public List<string> AllowedExtensions { get; set; } = new();

    public FilesConfig Copy()
    {
        return new FilesConfig
        {
            MinimumFileSize = MinimumFileSize,
            IgnoredFiles = new List<string>(IgnoredFiles ?? new List<string>()),
            IgnoredPatterns = new List<string>(IgnoredPatterns ?? new List<string>()),
            SkipHiddenFiles = SkipHiddenFiles,
            SkipSystemFiles = SkipSystemFiles,
            AllowedExtensions = new List<string>(AllowedExtensions ?? new List<string>())
        };
    }
} 