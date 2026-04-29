using Microsoft.EntityFrameworkCore;
using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Auth;
using Serilog;

namespace NineKgTools.Core.Services.Auth;

/// <summary>
/// 用户初始化服务，负责在应用启动时初始化默认用户
/// </summary>
public class UserInitializationService
{
    private readonly MediaDbContext _dbContext;
    private readonly PasswordService _passwordService;

    // 环境变量名称
    private const string EnvUser = "NT_USER";
    private const string EnvPassword = "NT_PASSWORD";

    // 默认凭据
    private const string DefaultUsername = "admin";
    private const string DefaultPassword = "admin";

    public UserInitializationService(MediaDbContext dbContext, PasswordService passwordService)
    {
        _dbContext = dbContext;
        _passwordService = passwordService;
    }

    /// <summary>
    /// 初始化默认用户（如果数据库中没有用户）
    /// </summary>
    public async Task InitializeDefaultUserAsync()
    {
        // 检查是否已有用户
        if (await _dbContext.Users.AnyAsync())
        {
            Log.Debug("数据库中已存在用户，跳过初始化");
            return;
        }

        // 从环境变量获取凭据，如果没有则使用默认值
        var username = Environment.GetEnvironmentVariable(EnvUser);
        var password = Environment.GetEnvironmentVariable(EnvPassword);

        var isFromEnv = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password);

        if (!isFromEnv)
        {
            username = DefaultUsername;
            password = DefaultPassword;
            Log.Information("未找到环境变量 {EnvUser}/{EnvPassword}，使用默认凭据创建初始用户: {Username}",
                EnvUser, EnvPassword, username);
        }
        else
        {
            Log.Information("从环境变量 {EnvUser}/{EnvPassword} 读取初始用户凭据: {Username}",
                EnvUser, EnvPassword, username);
        }

        var user = new User
        {
            Username = username!,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordService.HashPassword(user, password!);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        Log.Information("已创建初始用户: {Username}", username);
    }
}
