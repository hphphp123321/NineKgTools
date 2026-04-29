using YamlDotNet.Serialization;

namespace NineKgTools.Core.Services.Configs;

public class AppConfig
{
    [YamlMember(Alias = "web_host", Description = "主机地址, 例如: ::")]
    public string WebHost { get; set; } = null!;

    [YamlMember(Alias = "web_port", Description = "端口")]
    public int WebPort { get; set; } = 23333;

    [YamlMember(Alias = "proxy", Description = "代理设置")]
    public ProxyConfig Proxy { get; set; } = null!;

    [YamlMember(Alias = "user_agent", Description = "浏览器User-Agent")]
    public string? UserAgent { get; set; }


    public AppConfig Copy()
    {
        return new AppConfig
        {
            WebHost = WebHost,
            WebPort = WebPort,
            Proxy = Proxy,
            UserAgent = UserAgent,
        };
    }
}

public class ProxyConfig
{
    [YamlMember(Alias = "proxy_addr", Description = "代理地址，以http://或https://开头")]
    public string? ProxyAddr { get; set; }
    
    [YamlMember(Alias = "proxy_user", Description = "代理用户名")]
    public string? ProxyUser { get; set; }
    
    [YamlMember(Alias = "proxy_password", Description = "代理密码")]
    public string? ProxyPassword { get; set; }
    
    public ProxyConfig Copy()
    {
        return new ProxyConfig
        {
            ProxyAddr = ProxyAddr,
            ProxyUser = ProxyUser,
            ProxyPassword = ProxyPassword
        };
    }
}
