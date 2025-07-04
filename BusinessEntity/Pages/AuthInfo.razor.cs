using System.Collections.ObjectModel;
using BusinessEntity.Services;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ReactiveUI;
using System.Security.Claims;

namespace BusinessEntity.Pages
{
    public partial class AuthInfo : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<AuthInfo> Logger { get; set; } = default!;

        private string? CurrentUserName { get; set; }
        private string? CurrentUserEmail { get; set; }
        private string? CurrentUserId { get; set; }
        private string? JwtToken { get; set; }
        private ClaimsPrincipal? CurrentUser { get; set; }
        private List<Claim> AllClaims { get; set; } = new();
        private Dictionary<string, List<Claim>> ClaimsByType { get; set; } = new();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var isAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                
                if (isAuthenticated)
                {
                    // Получаем базовую информацию о пользователе
                    CurrentUserName = await AuthService.GetUserNameAsync();
                    CurrentUserEmail = await AuthService.GetUserEmailAsync();
                    JwtToken = await AuthService.GetJwtTokenAsync();
                    
                    // Получаем полный объект пользователя с клеймами
                    CurrentUser = await AuthService.GetCurrentUserAsync();
                    
                    if (CurrentUser?.Identity != null)
                    {
                        // Извлекаем все клеймы
                        AllClaims = CurrentUser.Claims.ToList();
                        
                        // Группируем клеймы по типам для удобного отображения
                        ClaimsByType = AllClaims
                            .GroupBy(c => c.Type)
                            .ToDictionary(g => g.Key, g => g.ToList());
                        
                        // Получаем ID пользователя из клеймов
                        CurrentUserId = CurrentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                       ?? CurrentUser.FindFirst("sub")?.Value;
                    }
                    
                    Logger.LogInformation($"User {CurrentUserName} accessed AuthInfo page with {AllClaims.Count} claims");
                }
                else
                {
                    Logger.LogInformation("Anonymous user accessed AuthInfo page - redirecting to login");
                    RedirectToLogin();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading user information on AuthInfo page");
            }
        }

        private void RedirectToLogin()
        {
            try
            {
                var loginUrl = AuthService.GetLoginUrl("/authinfo");
                Logger.LogInformation($"Redirecting user to Authentic login url={loginUrl}");
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error redirecting to login from AuthInfo");
            }
        }

        private Task SignOut()
        {
            try
            {
                Logger.LogInformation($"User {CurrentUserName} is requesting sign out from AuthInfo page");
                
                // Очищаем локальные данные
                CurrentUserName = null;
                CurrentUserEmail = null;
                
                // Принудительно обновляем компонент
                StateHasChanged();
                
                Logger.LogInformation("Redirecting to sign out page");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during sign out preparation from AuthInfo");
            }
            finally
            {
                // Перенаправляем на страницу выхода
                Navigation.NavigateTo("/auth/logout", forceLoad: true);
            }
            
            return Task.CompletedTask;
        }

        private string GetFriendlyClaimTypeName(string claimType)
        {
            return claimType switch
            {
                ClaimTypes.NameIdentifier => "ID пользователя",
                ClaimTypes.Name => "Имя пользователя", 
                ClaimTypes.Email => "Email",
                ClaimTypes.GivenName => "Имя",
                ClaimTypes.Surname => "Фамилия",
                ClaimTypes.Role => "Роль",
                ClaimTypes.AuthenticationMethod => "Метод аутентификации",
                "sub" => "Субъект (Subject)",
                "aud" => "Аудитория (Audience)",
                "iss" => "Издатель (Issuer)",
                "iat" => "Выдан в (Issued At)",
                "exp" => "Истекает (Expires)",
                "nbf" => "Не действителен до (Not Before)",
                "jti" => "JWT ID",
                "preferred_username" => "Предпочитаемое имя пользователя",
                "given_name" => "Имя",
                "family_name" => "Фамилия",
                "jwt_token" => "JWT Токен",
                _ => claimType
            };
        }
    }
}
