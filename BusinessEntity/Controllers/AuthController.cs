using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Services;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;

namespace BusinessEntity.Controllers
{
    /// <summary>
    /// Контроллер точек входа аутентификации.
    /// Login/Logout работают через единый AuthentikSessionManager.
    /// Callback завершает code-flow и создаёт локальную cookie-сессию.
    /// </summary>
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;
        private readonly AuthentikSessionManager _authService;
        private readonly IUserConnector _userConnector;

        public AuthController(
            ILogger<AuthController> logger,
            AuthentikSessionManager authService,
            IUserConnector userConnector)
        {
            _logger = logger;
            _authService = authService;
            _userConnector = userConnector;
        }

        /// <summary>
        /// Перенаправляет браузер на Authentik authorize endpoint.
        /// </summary>
        [HttpGet("login")]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            _logger.LogInformation("[AuthController.Login] Processing login request for returnUrl: {ReturnUrl}", returnUrl);
            
            // Если пользователь уже авторизован, перенаправляем
            if (User.Identity?.IsAuthenticated == true)
            {
                await _userConnector.EnsureCurrentUserAsync(HttpContext.RequestAborted);
                _logger.LogInformation("[AuthController.Login] User already authenticated, redirecting to: {ReturnUrl}", returnUrl ?? "/");
                return LocalRedirect(returnUrl ?? "/");
            }

            var loginUrl = _authService.GetLoginUrl(returnUrl);
            _logger.LogInformation("[AuthController.Login] Redirecting to Authentik login URL: {LoginUrl}", loginUrl);
            return Redirect(loginUrl);
        }

        /// <summary>
        /// Callback для Authentik authorization-code flow.
        /// </summary>
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("[AuthController.Callback] Authentication error from Authentik: {Error}", error);
                return Redirect($"/auth/error?message={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("[AuthController.Callback] No authorization code received");
                return Redirect("/auth/error?message=Authorization failed");
            }

            try
            {
                var returnUrl = await _authService.CompleteLoginAsync(code, state, HttpContext.RequestAborted);
                var localUser = await _userConnector.EnsureCurrentUserAsync(HttpContext.RequestAborted);
                _logger.LogInformation(
                    "[AuthController.Callback] Local user ensured. localUserId={LocalUserId} name={LocalUserName}",
                    localUser?.Id,
                    localUser?.ExternalId);
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthController.Callback] Unexpected exception during callback processing");
                return Redirect("/auth/error?message=Authentication processing failed");
            }
        }

        /// <summary>
        /// Завершает локальную сессию и инициирует logout в Authentik.
        /// </summary>
        [HttpGet("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            _logger.LogInformation("[AuthController.Logout] Initiating sign-out. User: {User}", User.Identity?.Name);
            var redirectUrl = await _authService.SignOutAsync(HttpContext.RequestAborted);
            return Redirect(string.IsNullOrWhiteSpace(redirectUrl) ? (returnUrl ?? "/auth/logged-out") : redirectUrl);
        }
    }
}
