using Microsoft.AspNetCore.Identity;
using NineKgTools.Core.Models.Auth;

namespace NineKgTools.Core.Services.Auth;

/// <summary>
/// 密码服务，负责密码的哈希和验证
/// 使用 ASP.NET Core Identity 的 PasswordHasher，内部采用 PBKDF2-SHA256 算法
/// </summary>
public class PasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    /// <summary>
    /// 对密码进行哈希
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <param name="password">明文密码</param>
    /// <returns>密码哈希值</returns>
    public string HashPassword(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <param name="password">明文密码</param>
    /// <returns>验证是否成功</returns>
    public bool VerifyPassword(User user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }

    /// <summary>
    /// 检查密码是否需要重新哈希（当使用旧算法时）
    /// </summary>
    /// <param name="user">用户实体</param>
    /// <param name="password">明文密码</param>
    /// <returns>是否需要重新哈希</returns>
    public bool NeedsRehash(User user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.SuccessRehashNeeded;
    }
}
