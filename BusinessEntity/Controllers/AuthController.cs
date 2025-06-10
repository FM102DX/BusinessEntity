using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
            // Если пользователь уже авторизован, перенаправляем
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(returnUrl ?? "/");
            }

            // Перенаправляем на страницу авторизации Authentic
            var loginUrl = _authService.GetLoginUrl(returnUrl);
            _logger.LogInformation($"Redirecting to Authentic login: {loginUrl}");
            
            return Redirect(loginUrl);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback(string? code, string? state, string? error, string? token)
        {

            _logger.LogInformation("[AuthController.Callback] Received callback from Authentic");
            _logger.LogInformation($"[AuthController.Callback] Code: {code}, State: {state}, Token: {token}, Error: {error}");

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError($"[AuthController.Callback] Authentication error from Authentic: {error}");
                return Redirect($"/auth/error?message={Uri.EscapeDataString(error)}");
            }
            _logger.LogInformation("[AuthController.Callback] P1");
            if (string.IsNullOrEmpty(token) && string.IsNullOrEmpty(code))
            {
                _logger.LogError("[AuthController.Callback] No authorization code or token received from Authentic");
                return Redirect("/auth/error?message=Authorization failed");
            }
            _logger.LogInformation("[AuthController.Callback] P2");
            try
            {
                string? jwtToken = token;

                // Если получили код авторизации, обмениваем его на токен
                if (string.IsNullOrEmpty(jwtToken) && !string.IsNullOrEmpty(code))
                {
                    jwtToken = await _authService.ExchangeCodeAsync(code);
                    _logger.LogInformation($"[AuthController.Callback] P2.1.A  -- code was {code} token={jwtToken}");
                }

                _logger.LogInformation("[AuthController.Callback] P3");

                if (string.IsNullOrEmpty(jwtToken))
                {
                    _logger.LogError("[AuthController.Callback] Failed to obtain JWT token");
                    return Redirect("/auth/error?message=Failed to obtain access token");
                }
                _logger.LogInformation("[AuthController.Callback] P4 -- теперь полученный токен надо валидировать");
                // Валидируем токен
                var isValid = await _authService.ValidateTokenAsync(jwtToken);
                if (!isValid)
                {
                    _logger.LogError("[AuthController.Callback] Invalid JWT token received");
                    return Redirect("/auth/error?message=Invalid token");
                }
                _logger.LogInformation("[AuthController.Callback] P5");
                // Парсим JWT токен для получения данных пользователя
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(jwtToken);
                
                var userName = jsonToken.Claims.FirstOrDefault(x => x.Type == "preferred_username" || x.Type == "name" || x.Type == "sub")?.Value ?? "Unknown User";
                var email = jsonToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
                var userId = jsonToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? Guid.NewGuid().ToString();

                // Создаем claims для аутентификации
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Name, userName),
                    new Claim("jwt_token", jwtToken)
                };

                if (!string.IsNullOrEmpty(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
                _logger.LogInformation("P6");
                // Добавляем все claims из JWT токена
                foreach (var claim in jsonToken.Claims)
                {
                    if (!claims.Any(c => c.Type == claim.Type))
                    {
                        claims.Add(new Claim(claim.Type, claim.Value));
                    }
                }

                // Создаем ClaimsIdentity и авторизуем пользователя
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };
                _logger.LogInformation("P7");
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                // Также сохраняем токен в куки для дополнительной безопасности
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false, // В продакшене должно быть true для HTTPS
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(24)
                };

                Response.Cookies.Append("jwt_token", jwtToken, cookieOptions);

                _logger.LogInformation($"User {userName} successfully authenticated via Authentic");

                // Перенаправляем на исходную страницу или главную
                var returnUrl = !string.IsNullOrEmpty(state) ? state : "/";
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing authentication callback");
                return Redirect("/auth/error?message=Authentication processing failed");
            }
        }

        [HttpGet("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _authService.SignOutAsync();
                _logger.LogInformation("User logged out successfully");
                return Redirect("/auth/logged-out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return Redirect("/auth/error?message=Logout failed");
            }
        }
    }

    public class TokenResponse
    {
        public string? access_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? refresh_token { get; set; }
    }
}