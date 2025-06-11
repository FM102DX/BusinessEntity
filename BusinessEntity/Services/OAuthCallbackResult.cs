using System.Security.Claims;

namespace BusinessEntity.Services
{
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
}