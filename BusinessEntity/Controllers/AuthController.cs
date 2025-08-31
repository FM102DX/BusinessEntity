using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Services;
using BusinessEntity.Contracts;

namespace BusinessEntity.Controllers
{
    /// <summary>
    /// Контроллер точек входа аутентификации.
    /// Login/Logout используют OpenID Connect; Callback оставлен для legacy OAuth.
    /// </summary>
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

        /// <summary>
        /// Инициирует OIDC-челлендж к провайдеру (Authentik). Если пользователь уже
        /// аутентифицирован, выполняет безопасный редирект на returnUrl или на '/'.
        /// </summary>
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

            // Запускаем OIDC-челлендж (Authentik)
            var props = new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl
            };
            _logger.LogInformation("[AuthController.Login] Challenging OIDC scheme with RedirectUri: {RedirectUri}", props.RedirectUri);
            return Challenge(props, OpenIdConnectDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// Legacy OAuth callback. В текущей схеме OIDC он не используется —
        /// обработку завершает middleware на /signin-oidc. Оставлен для обратной
        /// совместимости со старым ApplicationSideAuthService.
        /// </summary>
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

        /// <summary>
        /// Завершает сессию: локальный cookie и удалённую OIDC-сессию (front-channel).
        /// </summary>
        [HttpGet("logout")]
        [Authorize]
        public IActionResult Logout(string? returnUrl = null)
        {
            _logger.LogInformation("[AuthController.Logout] Initiating OIDC sign-out. User: {User}", User.Identity?.Name);
            var props = new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl
            };
            // Sign out both local cookie and remote OIDC session (front-channel)
            return SignOut(
                props,
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}