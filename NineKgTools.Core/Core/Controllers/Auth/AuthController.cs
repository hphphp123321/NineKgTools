using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NineKgTools.Core.Services.Auth;

namespace NineKgTools.Core.Controllers.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password, [FromForm] bool rememberMe = false)
    {
        var user = await _authService.ValidateCredentialsAsync(username, password);
        if (user == null)
        {
            return Unauthorized(new { message = "用户名或密码错误" });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, "User")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)  // 记住我：30天
                : DateTimeOffset.UtcNow.AddDays(1),  // 不记住：1天
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            claimsPrincipal,
            authProperties);

        return Ok(new { message = "登录成功" });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "登出成功" });
    }

    [Authorize]
    [HttpPost("change-username")]
    public async Task<IActionResult> ChangeUsername([FromForm] string currentPassword, [FromForm] string newUsername)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newUsername))
        {
            return BadRequest(new { message = "参数不完整" });
        }

        var (success, message) = await _authService.ChangeUsernameAsync(currentPassword, newUsername);

        if (!success)
        {
            return BadRequest(new { message });
        }

        // 修改用户名后需要重新登录
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message, requireRelogin = true });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromForm] string currentPassword, [FromForm] string newPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            return BadRequest(new { message = "参数不完整" });
        }

        var (success, message) = await _authService.ChangePasswordAsync(currentPassword, newPassword);

        if (!success)
        {
            return BadRequest(new { message });
        }

        // 修改密码后需要重新登录
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message, requireRelogin = true });
    }

    [Authorize]
    [HttpGet("current-user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _authService.GetCurrentUserAsync();
        if (user == null)
        {
            return NotFound(new { message = "用户不存在" });
        }

        return Ok(new
        {
            username = user.Username,
            createdAt = user.CreatedAt,
            lastLoginAt = user.LastLoginAt
        });
    }
}
