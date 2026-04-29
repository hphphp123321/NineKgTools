namespace NineKgTools.Core.Models.Auth;

/// <summary>
/// 用户实体
/// </summary>
public class User
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 用户名（唯一）
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// 密码哈希（使用 PBKDF2）
    /// </summary>
    public string PasswordHash { get; set; } = null!;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后登录时间
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
