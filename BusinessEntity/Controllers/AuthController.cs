using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Services;
using BusinessEntity.Contracts;

namespace BusinessEntity.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IApplicationSideAuthService _authService;
        private readonly IConfiguration _configuration;

        public AuthController(
            ILogger<AuthController> logger,
            IApplicationSideAuthService authService,
            IConfiguration configuration)
        {
            _logger = logger;
            _authService = authService;
            _configuration = configuration;
        }

        [HttpGet("login")]
        public IActionResult Login(string? returnUrl = null)
        {
            _logger.LogInformation("[AuthController.Login] Processing login request for returnUrl: {ReturnUrl}", returnUrl);
            
            // Если пользователь уже авторизован, перенаправляем
            if (User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("[AuthController.Login] User already authenticated, redirecting to: {ReturnUrl}", returnUrl ?? "/");
                return LocalRedirect(returnUrl ?? "/");
            }

            // Перенаправляем на страницу авторизации Authentic
            var loginUrl = _authService.GetLoginUrl(returnUrl);
            _logger.LogInformation("[AuthController.Login] Redirecting to Authentic login: {LoginUrl}", loginUrl);

            return Redirect(loginUrl);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            _logger.LogInformation("[AuthController.Callback] Received callback from Authentic");
            _logger.LogInformation("[AuthController.Callback] Params: code={Code}, state={State}, error={Error}",
                code, state, error);

            // Проверяем на ошибки OAuth
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("[AuthController.Callback] Authentication error from Authentic: {Error}", error);
                return Redirect($"/auth/error?message={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("[AuthController.Callback] No authorization code received");
                return Redirect("/auth/error?message=Authorization failed");
            }

            try
            {
                // Обрабатываем OAuth callback через сервис
                var result = await _authService.ProcessOAuthCallbackAsync(code);
                
                if (!result.IsSuccess)
                {
                    _logger.LogError("[AuthController.Callback] OAuth processing failed: {Error}", result.ErrorMessage);
                    return Redirect($"/auth/error?message={Uri.EscapeDataString(result.ErrorMessage ?? "Authentication failed")}");
                }

                // Выполняем SignIn
                _logger.LogInformation("[AuthController.Callback] Signing in user: {UserName}", result.UserName);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };
                
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, 
                    result.UserPrincipal!, authProperties);
                _logger.LogInformation("[AuthController.Callback] SignIn completed successfully");

                // Сохраняем токены в куки
                _logger.LogInformation("[AuthController.Callback] Setting authentication cookies");
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(24)
                };
                
                Response.Cookies.Append("jwt_token", result.Tokens!.IdToken, cookieOptions);
                Response.Cookies.Append("access_token", result.Tokens.AccessToken, cookieOptions);
                _logger.LogInformation("[AuthController.Callback] Authentication cookies set successfully");

                // Безопасное перенаправление
                var returnUrl = _authService.GetSafeReturnUrl(state);
                _logger.LogInformation("[AuthController.Callback] User {UserName} successfully authenticated, redirecting to: {ReturnUrl}", 
                    result.UserName, returnUrl);
                
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthController.Callback] Unexpected exception during callback processing");
                return Redirect("/auth/error?message=Authentication processing failed");
            }
        }

        [HttpGet("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("[AuthController.Logout] Processing logout request");
            _logger.LogInformation("[AuthController.Logout] User: {User}, Authenticated: {IsAuth}", 
                User.Identity?.Name, User.Identity?.IsAuthenticated);
            
            try
            {
                var success = await _authService.SignOutAsync();
                if (success)
                {
                    _logger.LogInformation("[AuthController.Logout] Logout completed successfully");
                    
                    // Проверяем, есть ли URL для фронт-ченнел logout
                    var frontChannelUrl = _authService.GetFrontChannelLogoutUrl();
                    if (!string.IsNullOrEmpty(frontChannelUrl))
                    {
                        _logger.LogInformation("[AuthController.Logout] Redirecting to front-channel logout: {Url}", frontChannelUrl);
                        return Redirect(frontChannelUrl); // Отправляем браузер в Authentik для очистки сессии
                    }
                    
                    return Redirect("/auth/logged-out"); // fallback
                }
                else
                {
                    _logger.LogError("[AuthController.Logout] Logout returned false");
                    return StatusCode(500, "Logout failed");
                }
            }
            catch (AuthSignOutFromAuthenticException ex)
            {
                _logger.LogError(ex, "[AuthController.Logout] Authentik logout failed");
                return StatusCode(500, "Failed to logout from authentication server. The logout process has been stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthController.Logout] Unexpected error during logout");
                return StatusCode(500, "Logout failed due to unexpected error");
            }
        }
    }
}