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
        public async Task<IActionResult> Callback(string? code, string? state, string? error)
        {
            // Шаг 1. Обрабатываем код авторизации
            _logger.LogInformation("[AuthController.Callback] Received callback from Authentic");
            _logger.LogInformation($"[AuthController.Callback] Code: {code}, State: {state}, Error: {error}");

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError($"[AuthController.Callback] Authentication error from Authentic: {error}");
                return Redirect($"/auth/error?message={Uri.EscapeDataString(error)}");
            }

            _logger.LogInformation("[AuthController.Callback] P1");
            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("[AuthController.Callback] No authorization code received from Authentic");
                return Redirect("/auth/error?message=Authorization failed");
            }

            try
            {
                // === Шаг 2. Меняем код на оба токена (access + id) ===
                var tokens = await _authService.ExchangeCodeAsync(code);
                _logger.LogInformation(
                    "[AuthController.Callback] P2.1 — code was {Code}, access_token={Access}, id_token={Id}",
                    code,
                    tokens.AccessToken,
                    tokens.IdToken);

                _logger.LogInformation("[AuthController.Callback] P2.2");

                if (string.IsNullOrEmpty(tokens.IdToken))
                {
                    _logger.LogError("[AuthController.Callback] Failed to obtain id_token");
                    return Redirect("/auth/error?message=Failed to obtain id_token");
                }

                // === Шаг 3. Валидируем access_token через introspect ===
                _logger.LogInformation("[AuthController.Callback] P3 — validating access_token via introspect");
                if (string.IsNullOrEmpty(tokens.AccessToken))
                {
                    _logger.LogWarning("[AuthController.Callback] Missing access_token");
                    return Redirect("/auth/error?message=Missing access token");
                }

                var isValid = await _authService.ValidateTokenAsync(tokens.AccessToken);
                if (!isValid)
                {
                    _logger.LogError("[AuthController.Callback] Invalid access_token received");
                    return Redirect("/auth/error?message=Invalid access token");
                }
                _logger.LogInformation("[AuthController.Callback] P4");

                // Шаг 4. Парсим id_token для данных пользователя
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(tokens.IdToken);
                var userName = jsonToken.Claims
                                    .FirstOrDefault(x => x.Type == "preferred_username"
                                                      || x.Type == "name"
                                                      || x.Type == "sub")?.Value
                                  ?? "Unknown User";
                var email = jsonToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
                var userId = jsonToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value
                                  ?? Guid.NewGuid().ToString();

                // … (далее формируем claims и авторизуем, как было) …

                var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
            new Claim("jwt_token", tokens.IdToken)
        };
                if (!string.IsNullOrEmpty(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }
                foreach (var claim in jsonToken.Claims)
                {
                    if (!claims.Any(c => c.Type == claim.Type))
                    {
                        claims.Add(new Claim(claim.Type, claim.Value));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };
                _logger.LogInformation("P5");
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(24)
                };
                Response.Cookies.Append("jwt_token", tokens.IdToken, cookieOptions);
                Response.Cookies.Append("access_token", tokens.AccessToken, cookieOptions);

                _logger.LogInformation($"User {userName} successfully authenticated via Authentic");

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