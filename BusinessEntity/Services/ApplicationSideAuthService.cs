using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;
using BusinessEntity.Controllers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessEntity.Services
{
    public interface IApplicationSideAuthService
    {
        Task<bool> IsUserAuthenticatedAsync();
        Task<ClaimsPrincipal?> GetCurrentUserAsync();
        Task<string?> GetUserNameAsync();
        Task<string?> GetUserEmailAsync();
        Task<string?> GetJwtTokenAsync();
        Task<bool> ValidateTokenAsync(string token);
        Task<TokenResponseAuthenticCustom> ExchangeCodeAsync(string code);
        Task SignOutAsync();
        string GetLoginUrl(string? returnUrl = null);
        Task<bool> IsServiceAvailableAsync();
        
        // Новые методы для обработки OAuth callback
        Task<OAuthCallbackResult> ProcessOAuthCallbackAsync(string code);
        Task<ClaimsPrincipal> CreateUserPrincipalAsync(TokenResponseAuthenticCustom tokens);
        string GetSafeReturnUrl(string? state);
    }
    
    public record TokenResponseAuthenticCustom(
        string AccessToken,
        string IdToken,
        string? RefreshToken = null
    );
    
    // Новый класс для результата обработки OAuth callback
    public class OAuthCallbackResult
    {
        public bool IsSuccess { get; set; }
        public ClaimsPrincipal? UserPrincipal { get; set; }
        public TokenResponseAuthenticCustom? Tokens { get; set; }
        public string? ErrorMessage { get; set; }
        public string? UserName { get; set; }
        
        public static OAuthCallbackResult Success(ClaimsPrincipal userPrincipal, TokenResponseAuthenticCustom tokens, string userName)
        {
            return new OAuthCallbackResult
            {
                IsSuccess = true,
                UserPrincipal = userPrincipal,
                Tokens = tokens,
                UserName = userName
            };
        }
        
        public static OAuthCallbackResult Failure(string errorMessage)
        {
            return new OAuthCallbackResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage
            };
        }
    }

    public class ApplicationSideAuthService : IApplicationSideAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ApplicationSideAuthService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;


        public ApplicationSideAuthService(
            IHttpContextAccessor httpContextAccessor,
            ILogger<ApplicationSideAuthService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<bool> IsUserAuthenticatedAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Identity?.IsAuthenticated ?? false;
        }

        public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
        {
            await Task.CompletedTask;
            return _httpContextAccessor.HttpContext?.User;
        }

        public async Task<string?> GetUserNameAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value;
        }

        public async Task<string?> GetUserEmailAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.FindFirst(ClaimTypes.Email)?.Value;
        }

        public async Task<string?> GetJwtTokenAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // Проверяем токен в куки
            var token = httpContext.Request.Cookies["jwt_token"];
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }

            // Проверяем в заголовке Authorization
            var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                // V1: берём настройки из секции AuthentIC2 и HTTP-клиент с этим профилем
                var cfg = _configuration.GetSection("AuthentIC2");
                var client = _httpClientFactory.CreateClient("AuthentIC2");

                // V2: добавляем HTTP Basic с client_id:client_secret
                var creds = $"{cfg["ClientId"]}:{cfg["ClientSecret"]}";
                var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(creds));
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", header);

                // V3: готовим тело только с токеном
                var content = new FormUrlEncodedContent(new Dictionary<string, string?>
                {
                    ["token"] = token
                });


                _logger.LogInformation("[ApplicationSideAuthService.ValidateTokenAsync] P1: делаем запрос introspect POST to {Base}{Path}", client.BaseAddress, "/application/o/introspect/");
                var response = await client.PostAsync("/application/o/introspect/", content);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[ApplicationSideAuthService.ValidateTokenAsync] P1.X Token introspection returned HTTP {StatusCode}", (int)response.StatusCode);
                    return false;
                }

                // V5: парсим ответ
                var body = await response.Content.ReadAsStringAsync();
                var payload = JsonSerializer.Deserialize<JsonElement>(body);
                _logger.LogInformation($"[ApplicationSideAuthService.ValidateTokenAsync] P2: парсим ответ body={body} payload={payload}");

                // V6: возвращаем поле "active"
                return payload.GetProperty("active").GetBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ApplicationSideAuthService.ValidateTokenAsync] Error introspecting JWT token");
                return false;
            }
        }


        public async Task<TokenResponseAuthenticCustom> ExchangeCodeAsync(string code)
        {
            _logger.LogInformation("P2.1");
            var cfg = _configuration.GetSection("AuthentIC2");
            var client = _httpClientFactory.CreateClient("AuthentIC2");

            _logger.LogInformation("P2.2");

            // 1) Формируем тело запроса без секрета
            var form = new Dictionary<string, string?>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = cfg["RedirectUri"],
                ["client_id"] = cfg["ClientId"]
            };
            var content = new FormUrlEncodedContent(form);

            _logger.LogInformation("P2.3");
            _logger.LogInformation(
                "base={Base} POST /application/o/token/  form-fields=[{Fields}]",
                client.BaseAddress,
                string.Join(", ", form.Keys));

            // 2) Добавляем Basic-авторизацию
            var creds = $"{cfg["ClientId"]}:{cfg["ClientSecret"]}";
            var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(creds));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", header);

            // 3) Отправляем
            var response = await client.PostAsync("/application/o/token/", content);

            _logger.LogInformation("P2.4");

            // 4) Читаем тело и логируем, если не 2xx
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Token endpoint returned HTTP {StatusCode}. Response body: {Body}",
                    (int)response.StatusCode,
                    responseBody);
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("P2.5");

            // 5) Парсим и возвращаем сразу access_token, id_token и (опционально) refresh_token
            var doc = JsonDocument.Parse(responseBody);
            var at = doc.RootElement.GetProperty("access_token").GetString()!;
            var idt = doc.RootElement.GetProperty("id_token").GetString()!;
            string? rft = null;
            if (doc.RootElement.TryGetProperty("refresh_token", out var p))
            {
                rft = p.GetString();
            }

            return new TokenResponseAuthenticCustom(at, idt, rft);
        }




        public string GetLoginUrl(string? returnUrl = null)
        {
            var cfg = _configuration.GetSection("AuthentIC");
            var baseUrl = cfg["BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            var clientId = cfg["ClientId"]!;
            var redirectUri = cfg["RedirectUri"]!;
            var scope = cfg["Scope"] ?? "openid profile email";
            _logger.LogInformation("*****");
            _logger.LogInformation("CONFIG ClientId = {ClientId}", _configuration["AuthentIC:ClientId"]);
            _logger.LogInformation("*****");

            // Генерируем state
            var state = string.IsNullOrEmpty(returnUrl)
                ? Guid.NewGuid().ToString("N")
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(returnUrl));

            var query = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = scope,
                ["state"] = state,
                ["prompt"] = "login consent", // Принудительно показывать форму входа и согласие
                ["max_age"] = "0", // Требовать повторную аутентификацию немедленно
                ["hd"] = "", // Пустое значение для принудительного выбора аккаунта
                ["include_granted_scopes"] = "false" // Не использовать ранее предоставленные разрешения
            };

            return QueryHelpers.AddQueryString(
                $"{baseUrl}/application/o/authorize/", query);
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient("AuthentIC");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await client.GetAsync("/health", cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Authentic service is available");
                    return true;
                }

                _logger.LogWarning($"Authentic service returned status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Authentic service availability");
                return false;
            }
        }

        public async Task SignOutAsync()
        {
            try
            {
                _logger.LogInformation("=== STARTING GLOBAL SIGN OUT PROCESS ===");
                
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    var userName = httpContext.User.Identity.Name;
                    var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    _logger.LogInformation("User to sign out: Name={UserName}, Id={UserId}", userName, userId);

                    // Сначала сохраняем токен ДО удаления cookie
                    var jwtToken = httpContext.Request.Cookies["jwt_token"];
                    var accessToken = httpContext.Request.Cookies["access_token"];
                    
                    _logger.LogInformation("Found tokens: JWT={HasJwt}, Access={HasAccess}", 
                        !string.IsNullOrEmpty(jwtToken), !string.IsNullOrEmpty(accessToken));
                    
                    if (!string.IsNullOrEmpty(jwtToken))
                    {
                        _logger.LogInformation("JWT token preview: {TokenStart}...{TokenEnd}", 
                            jwtToken.Substring(0, Math.Min(15, jwtToken.Length)),
                            jwtToken.Length > 15 ? jwtToken.Substring(jwtToken.Length - 15) : "");
                    }

                    // ШАГ 1: Сначала выполняем logout из Authentik
                    bool authentikLogoutSuccess = false;
                    if (!string.IsNullOrEmpty(jwtToken))
                    {
                        _logger.LogInformation("=== PHASE 1: Authentik logout ===");
                        authentikLogoutSuccess = await LogoutFromAuthentikAsync(jwtToken);
                        
                        if (!authentikLogoutSuccess)
                        {
                            _logger.LogWarning("Authentik logout failed, but continuing with local logout for user experience");
                        }
                        else
                        {
                            _logger.LogInformation("Successfully logged out from Authentik");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No JWT token found for Authentik logout - skipping remote logout");
                        authentikLogoutSuccess = true; // Считаем успешным, если нет токена
                    }

                    // ШАГ 2: Выполняем локальный logout (изменили логику - всегда выполняем)
                    _logger.LogInformation("=== PHASE 2: Local logout ===");
                    
                    // Выполняем выход из Cookie аутентификации
                    _logger.LogInformation("Signing out from Cookie authentication scheme");
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    
                    // Удаляем cookies
                    _logger.LogInformation("Deleting authentication cookies");
                    var cookieOptions = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(-1) // Устанавливаем в прошлое для принудительного удаления
                    };
                    
                    httpContext.Response.Cookies.Append("jwt_token", "", cookieOptions);
                    httpContext.Response.Cookies.Append("access_token", "", cookieOptions);
                    
                    // Также пробуем удалить стандартным способом
                    httpContext.Response.Cookies.Delete("jwt_token");
                    httpContext.Response.Cookies.Delete("access_token");

                    _logger.LogInformation("Local sign out completed successfully");
                    _logger.LogInformation("=== GLOBAL SIGN OUT COMPLETED ===");
                    _logger.LogInformation("Authentik logout success: {AuthentikSuccess}", authentikLogoutSuccess);
                }
                else
                {
                    _logger.LogInformation("No authenticated user to sign out");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ERROR DURING GLOBAL SIGN OUT ===");
                _logger.LogError("Exception type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception message: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Выполняет полный logout из Authentik через token revocation и logout endpoint
        /// </summary>
        /// <param name="token">JWT токен для отзыва</param>
        /// <returns>true если logout успешен, false если нет</returns>
        private async Task<bool> LogoutFromAuthentikAsync(string token)
        {
            try
            {
                _logger.LogInformation("=== STARTING AUTHENTIK LOGOUT PROCESS ===");
                _logger.LogInformation("Token to revoke: {TokenStart}...{TokenEnd}", 
                    token.Substring(0, Math.Min(10, token.Length)), 
                    token.Length > 10 ? token.Substring(token.Length - 10) : "");
                
                var cfg = _configuration.GetSection("AuthentIC2");
                var baseUrl = cfg["BaseUrl"];
                _logger.LogInformation("Using Authentik BaseUrl: {BaseUrl}", baseUrl);
                
                var client = _httpClientFactory.CreateClient("AuthentIC2");
                _logger.LogInformation("HTTP Client BaseAddress: {BaseAddress}", client.BaseAddress);

                bool revokeSuccess = false;
                bool logoutSuccess = false;

                // ШАГ 1: Проверяем доступность Authentik
                try
                {
                    _logger.LogInformation("=== STEP 0: Testing Authentik connectivity ===");
                    var healthRequest = new HttpRequestMessage(HttpMethod.Get, "/health");
                    var healthResponse = await client.SendAsync(healthRequest);
                    _logger.LogInformation("Health check result: {StatusCode}", healthResponse.StatusCode);
                    
                    if (!healthResponse.IsSuccessStatusCode)
                    {
                        var healthBody = await healthResponse.Content.ReadAsStringAsync();
                        _logger.LogWarning("Health check failed. Response: {Response}", healthBody);
                    }
                }
                catch (Exception healthEx)
                {
                    _logger.LogError(healthEx, "Health check failed with exception: {Message}", healthEx.Message);
                    // Продолжаем выполнение, возможно /health недоступен, но основные endpoints работают
                }

                // ШАГ 2: Token revocation
                try
                {
                    _logger.LogInformation("=== STEP 1: Token revocation ===");
                    var revokeEndpoint = "/application/o/revoke/";
                    
                    var creds = $"{cfg["ClientId"]}:{cfg["ClientSecret"]}";
                    var header = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(creds));
                    _logger.LogInformation("Using client credentials for: {ClientId}", cfg["ClientId"]);
                    
                    var revokeRequest = new HttpRequestMessage(HttpMethod.Post, revokeEndpoint);
                    revokeRequest.Headers.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", header);
                    
                    var revokeData = new Dictionary<string, string>
                    {
                        ["token"] = token,
                        ["token_type_hint"] = "access_token",
                        ["client_id"] = cfg["ClientId"]!
                    };
                    
                    revokeRequest.Content = new FormUrlEncodedContent(revokeData);
                    
                    _logger.LogInformation("Sending POST request to: {Url}", $"{baseUrl}{revokeEndpoint}");
                    _logger.LogInformation("Request headers: Authorization=Basic *****, Content-Type=application/x-www-form-urlencoded");
                    _logger.LogInformation("Request body keys: {Keys}", string.Join(", ", revokeData.Keys));
                    
                    var revokeResponse = await client.SendAsync(revokeRequest);
                    
                    revokeSuccess = revokeResponse.IsSuccessStatusCode;
                    _logger.LogInformation("Token revocation response: Status={StatusCode}, Success={Success}", 
                        revokeResponse.StatusCode, revokeSuccess);
                    
                    var revokeResponseBody = await revokeResponse.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(revokeResponseBody))
                    {
                        _logger.LogInformation("Token revocation response body: {Body}", revokeResponseBody);
                    }
                    
                    // Логируем заголовки ответа
                    foreach (var header01 in revokeResponse.Headers)
                    {
                        _logger.LogDebug("Response header: {Key} = {Value}", header01.Key, string.Join(", ", header01.Value));
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    _logger.LogError(httpEx, "HTTP error during token revocation: {Message}", httpEx.Message);
                    _logger.LogError("This usually indicates network connectivity issues or wrong BaseUrl");
                }
                catch (TaskCanceledException timeoutEx)
                {
                    _logger.LogError(timeoutEx, "Timeout during token revocation: {Message}", timeoutEx.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during token revocation: {Message}", ex.Message);
                }

                // ШАГ 3: Browser logout через OIDC End Session endpoint
                try
                {
                    _logger.LogInformation("=== STEP 2: Browser logout via OIDC End Session ===");
                    
                    var endSessionEndpoint = cfg["EndSessionEndpoint"] ?? "/application/o/end-session/";
                    _logger.LogInformation("Using End Session endpoint: {Endpoint}", endSessionEndpoint);
                    
                    // Формируем параметры для logout согласно OIDC спецификации
                    var endSessionParams = new Dictionary<string, string>
                    {
                        ["client_id"] = cfg["ClientId"]!,
                        ["post_logout_redirect_uri"] = cfg["RedirectUri"]!,
                        ["id_token_hint"] = token // Передаем ID token как hint
                    };
                    
                    var endSessionUrl = QueryHelpers.AddQueryString(endSessionEndpoint, endSessionParams);
                    _logger.LogInformation("Full End Session URL: {Url}", $"{baseUrl}{endSessionUrl}");
                    
                    var endSessionRequest = new HttpRequestMessage(HttpMethod.Get, endSessionUrl);
                    
                    var endSessionResponse = await client.SendAsync(endSessionRequest);
                    
                    logoutSuccess = endSessionResponse.IsSuccessStatusCode || 
                                   endSessionResponse.StatusCode == System.Net.HttpStatusCode.Redirect ||
                                   endSessionResponse.StatusCode == System.Net.HttpStatusCode.Found;
                    
                    _logger.LogInformation("End Session response: Status={StatusCode}, Success={Success}", 
                        endSessionResponse.StatusCode, logoutSuccess);
                    
                    if (logoutSuccess)
                    {
                        _logger.LogInformation("Successfully logged out using End Session endpoint");
                        
                        // Проверяем заголовки Location для редиректов
                        if (endSessionResponse.Headers.Location != null)
                        {
                            _logger.LogInformation("End Session redirect location: {Location}", endSessionResponse.Headers.Location);
                        }
                    }
                    else
                    {
                        var responseBody = await endSessionResponse.Content.ReadAsStringAsync();
                        if (!string.IsNullOrEmpty(responseBody))
                        {
                            var preview = responseBody.Length > 200 ? 
                                responseBody.Substring(0, 200) + "..." : responseBody;
                            _logger.LogDebug("End Session response preview: {Preview}", preview);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during OIDC End Session logout: {Message}", ex.Message);
                }

                // Итоговый результат
                var overallSuccess = revokeSuccess || logoutSuccess;
                _logger.LogInformation("=== AUTHENTIK LOGOUT SUMMARY ===");
                _logger.LogInformation("Token revocation success: {RevokeSuccess}", revokeSuccess);
                _logger.LogInformation("Browser logout success: {LogoutSuccess}", logoutSuccess);
                _logger.LogInformation("Overall logout success: {OverallSuccess}", overallSuccess);
                _logger.LogInformation("=== END AUTHENTIK LOGOUT PROCESS ===");
                
                return overallSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== FATAL ERROR IN AUTHENTIK LOGOUT ===");
                _logger.LogError("Exception type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception message: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                return false;
            }
        }

        public async Task<OAuthCallbackResult> ProcessOAuthCallbackAsync(string code)
        {
            try
            {
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step2 – Exchanging code for tokens");
                
                // Шаг 1. Обмен кода на токены
                var tokens = await ExchangeCodeAsync(code);
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step2.Result – access_token={Access}, id_token={Id}",
                    tokens.AccessToken, tokens.IdToken);

                if (string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.IdToken))
                {
                    _logger.LogError("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step2.Error – Missing one of the tokens");
                    return OAuthCallbackResult.Failure("Failed to obtain tokens");
                }

                // Шаг 2. Интроспекция access_token
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step3 – Validating access_token via introspection");
                var isValid = await ValidateTokenAsync(tokens.AccessToken);
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step3.Result – active={Active}", isValid);
                if (!isValid)
                {
                    _logger.LogError("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step3.Error – access_token is not active");
                    return OAuthCallbackResult.Failure("Invalid access token");
                }

                // Шаг 3. Создание ClaimsPrincipal
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Step4 – Creating user principal");
                var userPrincipal = await CreateUserPrincipalAsync(tokens);
                var userName = userPrincipal.Identity?.Name ?? "Unknown User";
                
                _logger.LogInformation("[ApplicationSideAuthService.ProcessOAuthCallbackAsync] User principal created for: {UserName}", userName);
                
                return OAuthCallbackResult.Success(userPrincipal, tokens, userName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ApplicationSideAuthService.ProcessOAuthCallbackAsync] Exception in OAuth callback processing");
                return OAuthCallbackResult.Failure("Authentication processing failed");
            }
        }

        public async Task<ClaimsPrincipal> CreateUserPrincipalAsync(TokenResponseAuthenticCustom tokens)
        {
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4 – Parsing id_token and building user principal");

            // 4.1 Парсинг JWT
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.1 – Parsing JWT from id_token");
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokens.IdToken);
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.1.Result – JWT parsed, issuer={Issuer}, subject={Subject}",
                jwt.Issuer, jwt.Subject);

            // 4.2 Извлечение базовых полей
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.2 – Extracting userName, email, userId");
            var userNameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username" || c.Type == "name" || c.Type == "sub");
            var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == "email");
            var userName = userNameClaim?.Value ?? "Unknown User";
            var email = emailClaim?.Value;
            var userId = jwt.Subject ?? Guid.NewGuid().ToString();
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.2.Result – userId={UserId}, userName={UserName}, email={Email}",
                userId, userName, email);

            // 4.3 Составляем список claims
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.3 – Building claim list");
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
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.3.Result – total claims count={Count}", claims.Count);

            // 4.4 Создаем ClaimsIdentity и Principal
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.4 – Creating ClaimsIdentity and principal");
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            _logger.LogInformation("[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.4.Result – principal.Identity.Name={Name}",
                principal.Identity?.Name);

            // 4.4.1 Логируем все клеймы одной строкой, разделённые переводом строки
            _logger.LogInformation(
                "[ApplicationSideAuthService.CreateUserPrincipalAsync] Step4.4.1 – All claims:\n{AllClaims}",
                string.Join(
                    Environment.NewLine,
                    principal.Claims.Select(c => $"{c.Type} = {c.Value}")
                )
            );

            return await Task.FromResult(principal);
        }

        public string GetSafeReturnUrl(string? state)
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
                
                // Проверяем, что URL является локальным (простая проверка)
                if (decodedUrl.StartsWith("/") && !decodedUrl.StartsWith("//"))
                {
                    _logger.LogInformation("[ApplicationSideAuthService.GetSafeReturnUrl] Using decoded return URL: {ReturnUrl}", decodedUrl);
                    return decodedUrl;
                }
                else
                {
                    _logger.LogWarning("[ApplicationSideAuthService.GetSafeReturnUrl] Decoded URL is not local: {DecodedUrl}, using default", decodedUrl);
                    return defaultUrl;
                }
            }
            catch (Exception decodeEx)
            {
                _logger.LogWarning(decodeEx, "[ApplicationSideAuthService.GetSafeReturnUrl] Failed to decode state as Base64, trying as plain URL");
                
                // Если не удалось декодировать как Base64, пробуем как обычный URL
                if (state.StartsWith("/") && !state.StartsWith("//"))
                {
                    _logger.LogInformation("[ApplicationSideAuthService.GetSafeReturnUrl] Using plain return URL: {ReturnUrl}", state);
                    return state;
                }
                else
                {
                    _logger.LogWarning("[ApplicationSideAuthService.GetSafeReturnUrl] State is not a local URL: {State}, using default", state);
                    return defaultUrl;
                }
            }
        }
    }
}
