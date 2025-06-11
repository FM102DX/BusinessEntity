using System.Security.Claims;
using System.Threading.Tasks;

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
}