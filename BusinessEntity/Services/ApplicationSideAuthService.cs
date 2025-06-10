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
    }
    public record TokenResponseAuthenticCustom(
        string AccessToken,
        string IdToken,
        string? RefreshToken = null
    );
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
                ["state"] = state
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
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext?.User?.Identity?.IsAuthenticated == true)
                {
                    var userName = httpContext.User.Identity.Name;
                    _logger.LogInformation($"Signing out user: {userName}");

                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    httpContext.Response.Cookies.Delete("jwt_token");

                    // Необязательное уведомление Authentik
                    var token = httpContext.Request.Cookies["jwt_token"];
                    if (!string.IsNullOrEmpty(token))
                    {
                        var client = _httpClientFactory.CreateClient("AuthentIC");
                        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
                        request.Headers.Add("Authorization", $"Bearer {token}");
                        await client.SendAsync(request);
                    }

                    _logger.LogInformation("Sign out completed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
                throw;
            }
        }
    }
}
