using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace NineKgTools.Pages.Auth;

public partial class Login
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    [SupplyParameterFromQuery]
    [Parameter]
    public string? ReturnUrl { get; set; }

    // 用 property 包装字段，输入变化时自动清空错误提示
    private string _usernameValue = "";
    private string Username
    {
        get => _usernameValue;
        set { _usernameValue = value; _errorMessage = ""; }
    }

    private string _passwordValue = "";
    private string Password
    {
        get => _passwordValue;
        set { _passwordValue = value; _errorMessage = ""; }
    }

    private bool _rememberMe;
    private bool _isLoading;
    private bool _formValid;
    private string _errorMessage = "";
    private MudForm _form = null!;

    // autocomplete 属性，帮助密码管理器识别字段
    private readonly Dictionary<string, object> _usernameAttrs = new() { { "autocomplete", "username" } };
    private readonly Dictionary<string, object> _passwordAttrs = new() { { "autocomplete", "current-password" } };

    // 密码可见性
    private bool _isPasswordVisible;
    private InputType _passwordInputType = InputType.Password;
    private string _passwordIcon = Icons.Material.Filled.VisibilityOff;

    private void TogglePasswordVisibility()
    {
        _isPasswordVisible = !_isPasswordVisible;
        _passwordInputType = _isPasswordVisible ? InputType.Text : InputType.Password;
        _passwordIcon = _isPasswordVisible
            ? Icons.Material.Filled.Visibility
            : Icons.Material.Filled.VisibilityOff;
    }

    // 密码框 Enter 键提交
    private async Task HandlePasswordKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !_isLoading)
            await HandleLogin();
    }

    private async Task HandleLogin()
    {
        if (_isLoading) return;

        _errorMessage = "";
        _isLoading = true;

        try
        {
            await _form.Validate();
            if (!_formValid)
            {
                _isLoading = false;
                return;
            }

            // 使用 JavaScript 在浏览器端发送请求，确保 Cookie 正确设置到浏览器
            var result = await JsRuntime.InvokeAsync<LoginResult>("authInterop.login", _usernameValue, _passwordValue, _rememberMe);

            if (result.Success)
            {
                Snackbar.Add("登录成功", Severity.Success);
                // 验证 ReturnUrl 必须为本地相对路径，防止 Open Redirect 攻击
                var redirectUrl = IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/";
                NavigationManager.NavigateTo(redirectUrl, true);
            }
            else
            {
                _errorMessage = "用户名或密码错误，请重新输入";
            }
        }
        catch (Exception)
        {
            _errorMessage = "登录遇到问题，请稍后重试";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// 验证是否为本地相对 URL，防止 Open Redirect 攻击。
    /// 必须以 / 开头但不以 // 开头（protocol-relative URL 会跳转到外部站点）。
    /// </summary>
    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.StartsWith('/') && !url.StartsWith("//");
    }

    private record LoginResult(bool Success, int Status);
}
