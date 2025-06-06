using System.Security.Claims;

namespace BusinessEntity.Services
{
    public interface IAuterlinkAuthService
    {
        Task<bool> IsUserAuthenticatedAsync();
        Task<ClaimsPrincipal?> GetCurrentUserAsync();
        Task<string?> GetUserNameAsync();
        Task<string?> GetUserEmailAsync();
        Task SignOutAsync();
        string GetLoginUrl();
        Task<bool> IsServiceAvailableAsync();
    }

    public class AuterlinkAuthService : IAuterlinkAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuterlinkAuthService> _logger;
        private readonly HttpClient _httpClient;

        public AuterlinkAuthService(IHttpContextAccessor httpContextAccessor, ILogger<AuterlinkAuthService> logger, HttpClient httpClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<bool> IsUserAuthenticatedAsync()
        {
            var user = await GetCurrentUserAsync();
            return user?.Identity?.IsAuthenticated ?? false;
        }

        public async Task<ClaimsPrincipal?> GetCurrentUserAsync()
        {
            await Task.CompletedTask; // Для async совместимости
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

        public async Task SignOutAsync()
        {
            // TODO: Реализовать выход через Auterlink
            _logger.LogInformation("User signed out");
            await Task.CompletedTask;
        }

        public string GetLoginUrl()
        {
            // TODO: Настроить URL для входа через Auterlink
            return "/auterlink/login";
        }

        public async Task<bool> IsServiceAvailableAsync()
        {
            try
            {
                // Проверяем доступность сервиса Auterlink через простой HTTP-запрос
                // В реальной реализации это должен быть endpoint для проверки здоровья сервиса
                var healthCheckUrl = "http://localhost:5080/api/health"; // Пример URL для проверки
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // Таймаут 5 секунд
                var response = await _httpClient.GetAsync(healthCheckUrl, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Auterlink auth service is available");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Auterlink auth service returned status: {response.StatusCode}");
                    return false;
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("Timeout while checking Auterlink auth service availability");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while checking Auterlink auth service availability");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while checking Auterlink auth service availability");
                return false;
            }
        }
    }
}