using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace BusinessEntity.Pages
{
    [AllowAnonymous]
    public partial class Signout : ComponentBase
    {
        [Inject] public ILogger<Signout> Logger { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var httpContext = HttpContextAccessor.HttpContext;
                if (httpContext != null)
                {
                    if (httpContext.User.Identity?.IsAuthenticated == true)
                    {
                        var userName = httpContext.User.Identity.Name;
                        Logger.LogInformation($"Signing out user: {userName}");
                        
                        // Выполняем реальный выход из cookie-аутентификации
                        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        
                        Logger.LogInformation($"User {userName} successfully signed out");
                    }
                    else
                    {
                        Logger.LogInformation("No authenticated user to sign out");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during sign out process");
            }
            finally
            {
                // Перенаправляем на страницу подтверждения выхода
                Navigation.NavigateTo("/logout", true);
            }
        }
    }
} 