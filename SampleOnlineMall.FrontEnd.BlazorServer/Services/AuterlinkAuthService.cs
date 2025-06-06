using System.Security.Claims;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Services
{
    public interface IAuterlinkAuthService
    {
        Task<bool> IsUserAuthenticatedAsync();
        Task<ClaimsPrincipal?> GetCurrentUserAsync();
        Task<string?> GetUserNameAsync();
        Task<string?> GetUserEmailAsync();
        Task SignOutAsync();
        string GetLoginUrl();
    }

    public class AuterlinkAuthService : IAuterlinkAuthService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuterlinkAuthService> _logger;

        public AuterlinkAuthService(IHttpContextAccessor httpContextAccessor, ILogger<AuterlinkAuthService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
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
    }
}