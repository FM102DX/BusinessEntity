using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessEntity.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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
            _logger.LogInformation("[AuthController.Callback] Step1 – Params: code={Code}, state={State}, error={Error}",
                code, state, error);

            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("[AuthController.Callback] Step1.Error – Authentication error from Authentic: {Error}", error);
                return Redirect($"/auth/error?message={Uri.EscapeDataString(error)}");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogError("[AuthController.Callback] Step1.Error – No authorization code received");
                return Redirect("/auth/error?message=Authorization failed");
            }

            try
            {
                // Шаг 2. Обмен кода на токены
                _logger.LogInformation("[AuthController.Callback] Step2 – Exchanging code for tokens");
                var tokens = await _authService.ExchangeCodeAsync(code);
                _logger.LogInformation("[AuthController.Callback] Step2.Result – access_token={Access}, id_token={Id}",
                    tokens.AccessToken, tokens.IdToken);

                if (string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.IdToken))
                {
                    _logger.LogError("[AuthController.Callback] Step2.Error – Missing one of the tokens");
                    return Redirect("/auth/error?message=Failed to obtain tokens");
                }

                // Шаг 3. Интроспекция access_token
                _logger.LogInformation("[AuthController.Callback] Step3 – Validating access_token via introspection");
                var isValid = await _authService.ValidateTokenAsync(tokens.AccessToken);
                _logger.LogInformation("[AuthController.Callback] Step3.Result – active={Active}", isValid);
                if (!isValid)
                {
                    _logger.LogError("[AuthController.Callback] Step3.Error – access_token is not active");
                    return Redirect("/auth/error?message=Invalid access token");
                }

                // Шаг 4. Работа с id_token и формирование ClaimsPrincipal
                _logger.LogInformation("[AuthController.Callback] Step4 – Parsing id_token and building user principal");

                // 4.1 Парсинг JWT
                _logger.LogInformation("[AuthController.Callback] Step4.1 – Parsing JWT from id_token");
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(tokens.IdToken);
                _logger.LogInformation("[AuthController.Callback] Step4.1.Result – JWT parsed, issuer={Issuer}, subject={Subject}",
                    jwt.Issuer, jwt.Subject);

                // 4.2 Извлечение базовых полей
                _logger.LogInformation("[AuthController.Callback] Step4.2 – Extracting userName, email, userId");
                var userNameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == "name" || c.Type == "sub");
                var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == "email");
                var userName = userNameClaim?.Value ?? "Unknown User";
                var email = emailClaim?.Value;
                var userId = jwt.Subject ?? Guid.NewGuid().ToString();
                _logger.LogInformation("[AuthController.Callback] Step4.2.Result – userId={UserId}, userName={UserName}, email={Email}",
                    userId, userName, email);

                // 4.3 Составляем список claims
                _logger.LogInformation("[AuthController.Callback] Step4.3 – Building claim list");
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
                foreach (var cl in jwt.Claims)
                {
                    if (!claims.Any(c => c.Type == cl.Type && c.Value == cl.Value))
                    {
                        claims.Add(new Claim(cl.Type, cl.Value));
                    }
                }
                _logger.LogInformation("[AuthController.Callback] Step4.3.Result – total claims count={Count}", claims.Count);

                // 4.4 Создаем ClaimsIdentity и Principal
                _logger.LogInformation("[AuthController.Callback] Step4.4 – Creating ClaimsIdentity and principal");
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                _logger.LogInformation("[AuthController.Callback] Step4.4.Result – principal.Identity.Name={Name}",
                    principal.Identity?.Name);

                // 4.4.1 Логируем все клеймы одной строкой, разделённые переводом строки
                _logger.LogInformation(
                    "[AuthController.Callback] Step4.4.1 – All claims:\n{AllClaims}",
                    string.Join(
                        Environment.NewLine,
                        principal.Claims.Select(c => $"{c.Type} = {c.Value}")
                    )
                );

                // 4.5 Выполняем SignIn
                _logger.LogInformation("[AuthController.Callback] Step4.5 – Signing in user");
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24)
                };
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
                _logger.LogInformation("[AuthController.Callback] Step4.5.Result – SignIn complete");

                // 4.6 Сохраняем куки
                _logger.LogInformation("[AuthController.Callback] Step4.6 – Setting cookies for tokens");
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = false,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(24)
                };
                Response.Cookies.Append("jwt_token", tokens.IdToken, cookieOptions);
                Response.Cookies.Append("access_token", tokens.AccessToken, cookieOptions);
                _logger.LogInformation("[AuthController.Callback] Step4.6.Result – Cookies set");

                // Финальный шаг - безопасное перенаправление
                _logger.LogInformation("[AuthController.Callback] User {UserName} successfully authenticated", userName);
                
                var returnUrl = GetSafeReturnUrl(state);
                _logger.LogInformation("[AuthController.Callback] Redirecting to: {ReturnUrl}", returnUrl);
                return LocalRedirect(returnUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AuthController.Callback] Exception in callback processing");
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
                return Redirect("/"); // Перенаправляем на главную страницу вместо специальной страницы
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return Redirect("/auth/error?message=Logout failed");
            }
        }

        /// <summary>
        /// Безопасно обрабатывает returnUrl из state параметра OAuth callback
        /// </summary>
        /// <param name="state">Параметр state из OAuth callback</param>
        /// <returns>Безопасный локальный URL для перенаправления</returns>
        private string GetSafeReturnUrl(string? state)
        {
            const string defaultUrl = "/";
            
            if (string.IsNullOrEmpty(state))
            {
                return defaultUrl;
            }

            try
            {
                // Пробуем декодировать state как Base64
                var decodedBytes = Convert.FromBase64String(state);
                var decodedUrl = Encoding.UTF8.GetString(decodedBytes);
                
                // Проверяем, что URL является локальным
                if (Url.IsLocalUrl(decodedUrl))
                {
                    _logger.LogInformation("[GetSafeReturnUrl] Using decoded return URL: {ReturnUrl}", decodedUrl);
                    return decodedUrl;
                }
                else
                {
                    _logger.LogWarning("[GetSafeReturnUrl] Decoded URL is not local: {DecodedUrl}, using default", decodedUrl);
                    return defaultUrl;
                }
            }
            catch (Exception decodeEx)
            {
                _logger.LogWarning(decodeEx, "[GetSafeReturnUrl] Failed to decode state as Base64, trying as plain URL");
                
                // Если не удалось декодировать как Base64, пробуем как обычный URL
                if (Url.IsLocalUrl(state))
                {
                    _logger.LogInformation("[GetSafeReturnUrl] Using plain return URL: {ReturnUrl}", state);
                    return state;
                }
                else
                {
                    _logger.LogWarning("[GetSafeReturnUrl] State is not a local URL: {State}, using default", state);
                    return defaultUrl;
                }
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