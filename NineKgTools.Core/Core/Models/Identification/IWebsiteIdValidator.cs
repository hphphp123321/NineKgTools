using System.Text.RegularExpressions;

namespace NineKgTools.Core.Models.Identification;

/// <summary>
/// 网站ID验证器接口
/// </summary>
public interface IWebsiteIdValidator
{
    /// <summary>
    /// 验证ID是否有效
    /// </summary>
    bool IsValidId(string id);
    
    /// <summary>
    /// 获取ID格式说明
    /// </summary>
    string GetIdFormat();
    
    /// <summary>
    /// 获取ID示例
    /// </summary>
    string GetIdExample();
}

/// <summary>
/// DLsite ID验证器
/// </summary>
public class DLsiteIdValidator : IWebsiteIdValidator
{
    private static readonly Regex IdPattern = new(@"^[RVB]J\d{6,8}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    public bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;
            
        return IdPattern.IsMatch(id);
    }
    
    public string GetIdFormat() => "RJ/VJ/BJ + 6-8位数字";
    
    public string GetIdExample() => "RJ01081508";
}

/// <summary>
/// Bangumi ID验证器
/// </summary>
public class BangumiIdValidator : IWebsiteIdValidator
{
    public bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return int.TryParse(id, out var numId) && numId > 0;
    }

    public string GetIdFormat() => "纯数字ID";

    public string GetIdExample() => "22905";
}

/// <summary>
/// Steam AppID 验证器（纯数字）
/// </summary>
public class SteamIdValidator : IWebsiteIdValidator
{
    public bool IsValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        return int.TryParse(id, out var numId) && numId > 0;
    }

    public string GetIdFormat() => "Steam AppID（纯数字）";

    public string GetIdExample() => "730";
}

/// <summary>
/// 网站ID验证器管理器
/// </summary>
public static class WebsiteIdValidatorManager
{
    private static readonly Dictionary<string, IWebsiteIdValidator> Validators = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DLsite", new DLsiteIdValidator() },
        { "Bangumi", new BangumiIdValidator() },
        { "Steam", new SteamIdValidator() }
    };
    
    /// <summary>
    /// 获取指定网站的验证器
    /// </summary>
    public static IWebsiteIdValidator? GetValidator(string websiteName)
    {
        return Validators.GetValueOrDefault(websiteName);
    }
    
    /// <summary>
    /// 验证网站ID
    /// </summary>
    public static bool ValidateId(string websiteName, string id)
    {
        var validator = GetValidator(websiteName);
        return validator?.IsValidId(id) ?? !string.IsNullOrWhiteSpace(id);
    }
    
    /// <summary>
    /// 获取ID格式说明
    /// </summary>
    public static string GetIdFormat(string websiteName)
    {
        var validator = GetValidator(websiteName);
        return validator?.GetIdFormat() ?? "请输入有效的ID";
    }
    
    /// <summary>
    /// 获取ID示例
    /// </summary>
    public static string GetIdExample(string websiteName)
    {
        var validator = GetValidator(websiteName);
        return validator?.GetIdExample() ?? "";
    }
    
    /// <summary>
    /// 注册新的验证器
    /// </summary>
    public static void RegisterValidator(string websiteName, IWebsiteIdValidator validator)
    {
        Validators[websiteName] = validator;
    }
    
    /// <summary>
    /// 获取所有支持的网站
    /// </summary>
    public static IEnumerable<string> GetSupportedWebsites()
    {
        return Validators.Keys;
    }
}