using System.Security.Claims;
using System.Threading.Tasks;

namespace BusinessEntity.Services
{
    public interface IApplicationSideAuthService
    {
        // User info
        Task<bool> IsUserAuthenticatedAsync();
        Task<ClaimsPrincipal?> GetCurrentUserAsync();
        Task<string?> GetUserNameAsync();
        Task<string?> GetUserEmailAsync();
        Task<string?> GetJwtTokenAsync();

        // Token operations
        Task<bool> ValidateTokenAsync(string token);
        Task<TokenResponseAuthenticCustom> ExchangeCodeAsync(string code);

        // OAuth callback flow
        Task<OAuthCallbackResult> ProcessOAuthCallbackAsync(string code);
        Task<ClaimsPrincipal> CreateUserPrincipalAsync(TokenResponseAuthenticCustom tokens);
        string GetSafeReturnUrl(string? state);

        // Login / Logout
        string GetLoginUrl(string? returnUrl = null);
        Task<bool> SignOutAsync(); // Изменено: теперь возвращает bool
        /// <summary>URL на который нужно перенаправить браузер для полного выхода из Authentik</summary>
        string? GetFrontChannelLogoutUrl();

        // Health check
        Task<bool> IsServiceAvailableAsync();
    }
}