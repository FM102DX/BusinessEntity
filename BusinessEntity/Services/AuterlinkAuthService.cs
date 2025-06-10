using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Options;

namespace BusinessEntity.Services
{
    public interface IAuterlinkAuthService
    {
        Task<bool> IsUserAuthenticatedAsync();
        Task<ClaimsPrincipal?> GetCurrentUserAsync();
        Task<string?> GetUserNameAsync();
        Task<string?> GetUserEmailAsync();
        Task SignOutAsync();
        string GetLoginUrl(string? returnUrl = null);
        Task<bool> IsServiceAvailableAsync();
        Task<string?> GetJwtTokenAsync();
        Task<bool> ValidateTokenAsync(string token);
    }

    public class AuterlinkAuthService : IAuterlinkAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuterlinkAuthService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuterlinkAuthService(
            IHttpContextAccessor httpContextAccessor, 
            ILogger<AuterlinkAuthService> logger, 
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
                var httpClient = _httpClientFactory.CreateClient("AuthentIC");
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/validate");
                request.Headers.Add("Authorization", $"Bearer {token}");

                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating JWT token");
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
                    _logger.LogInformation($"Initiating sign out process for user: {userName}");

                    // Выполняем выход из Cookie аутентификации
                    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    // Удаляем JWT токен из куки
                    httpContext.Response.Cookies.Delete("jwt_token");
                    
                    // Уведомляем Authentic о выходе (опционально)
                    try
                    {
                        var token = httpContext.Request.Cookies["jwt_token"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            var httpClient = _httpClientFactory.CreateClient("AuthentIC");
                            var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
                            request.Headers.Add("Authorization", $"Bearer {token}");
                            await httpClient.SendAsync(request);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to notify Authentic about logout");
                    }
                }
                
                _logger.LogInformation("Sign out process completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while performing sign out");
                throw;
            }
        }

        public string GetLoginUrl(string? returnUrl = null)
        {
            var authenticBaseUrl = _configuration["AuthentIC:BaseUrl"] ?? "http://localhost:9000";
            var clientId = _configuration["AuthentIC:ClientId"] ?? "business-entity";
            var redirectUri = _configuration["AuthentIC:RedirectUri"]!; // Убираем fallback
            
            var loginUrl = $"{authenticBaseUrl}/application/o/authorize/?client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}";
            
            if (!string.IsNullOrEmpty(returnUrl))
            {
                loginUrl += $"&state={Uri.EscapeDataString(returnUrl)}";
            }
            
            return loginUrl;
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("AuthentIC");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await httpClient.GetAsync("/health", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Authentic auth service is available");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Authentic auth service returned status: {response.StatusCode}");
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("Timeout while checking Authentic auth service availability");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while checking Authentic auth service availability");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking Authentic auth service availability");
                return false;
            }
        }
    }
}