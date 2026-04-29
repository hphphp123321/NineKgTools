using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Auth;
using Serilog;

namespace NineKgTools.Core.Services.Auth;

/// <summary>
/// 认证服务，处理用户登录验证和密码管理
/// </summary>
public class AuthService
{
    private readonly MediaDbContext _dbContext;
    private readonly PasswordService _passwordService;

    public AuthService(MediaDbContext dbContext, PasswordService passwordService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
    }

    /// <summary>
    /// 验证用户凭据
    /// </summary>
    /// <param name="username">用户名</param>
    /// <param name="password">明文密码</param>
    /// <returns>验证成功返回用户实体，失败返回 null</returns>
    public async Task<User?> ValidateCredentialsAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

        if (user == null)
            return null;

        if (!_passwordService.VerifyPassword(user, password))
            return null;

        // 更新最后登录时间
        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // 检查是否需要重新哈希密码（算法升级）
        if (_passwordService.NeedsRehash(user, password))
        {
            user.PasswordHash = _passwordService.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            Log.Debug("已为用户 {Username} 更新密码哈希", username);
        }

        return user;
    }

    /// <summary>
    /// 获取当前用户（系统只有一个用户）
    /// </summary>
    public async Task<User?> GetCurrentUserAsync()
    {
        return await _dbContext.Users.FirstOrDefaultAsync();
    }

    /// <summary>
    /// 修改用户名（需要验证原密码）
    /// </summary>
    /// <param name="currentPassword">当前密码</param>
    /// <param name="newUsername">新用户名</param>
    /// <returns>操作结果</returns>
    public async Task<(bool Success, string Message)> ChangeUsernameAsync(
        string currentPassword, string newUsername)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync();
        if (user == null)
            return (false, "用户不存在");

        // 验证原密码
        if (!_passwordService.VerifyPassword(user, currentPassword))
            return (false, "原密码错误");

        // 检查新用户名格式
        if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 2 || newUsername.Length > 50)
            return (false, "用户名长度必须在 2-50 个字符之间");

        // 检查新用户名是否已存在
        if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == newUsername.ToLower() && u.Id != user.Id))
            return (false, "用户名已存在");

        user.Username = newUsername;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Log.Information("用户名已修改为: {Username}", newUsername);
        return (true, "用户名修改成功");
    }

    /// <summary>
    /// 修改密码（需要验证原密码）
    /// </summary>
    /// <param name="currentPassword">当前密码</param>
    /// <param name="newPassword">新密码</param>
    /// <returns>操作结果</returns>
    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        string currentPassword, string newPassword)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync();
        if (user == null)
            return (false, "用户不存在");

        // 验证原密码
        if (!_passwordService.VerifyPassword(user, currentPassword))
            return (false, "原密码错误");

        // 检查新密码格式
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
            return (false, "新密码长度至少为 4 个字符");

        // 更新密码
        user.PasswordHash = _passwordService.HashPassword(user, newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        Log.Information("用户 {Username} 的密码已修改", user.Username);
        return (true, "密码修改成功");
    }
}
