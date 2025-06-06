using Microsoft.AspNetCore.Components;
using BusinessEntity.Services;
using Microsoft.Extensions.Logging;

namespace BusinessEntity.Components
{
    public partial class RedirectToLogin : ComponentBase
    {
        [Parameter] public string? ReturnUrl { get; set; }
        
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public IAuterlinkAuthService AuthService { get; set; } = default!;
        [Inject] public ILogger<RedirectToLogin> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Сначала проверяем, доступен ли сервис авторизации
                var isServiceAvailable = await AuthService.IsServiceAvailableAsync();
                
                if (!isServiceAvailable)
                {
                    Logger.LogError("Auterlink auth service is not available during login redirect");
                    Navigation.NavigateTo("/auth-service-unavailable", true);
                    return;
                }

                // Если сервис доступен, перенаправляем на страницу входа
                var loginUrl = AuthService.GetLoginUrl();
                
                if (!string.IsNullOrEmpty(ReturnUrl))
                {
                    loginUrl += $"?returnUrl={Uri.EscapeDataString(ReturnUrl)}";
                }

                Logger.LogInformation($"Redirecting to login page: {loginUrl}");
                Navigation.NavigateTo(loginUrl, true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during login redirect or checking auth service availability");
                Navigation.NavigateTo("/auth-service-unavailable", true);
            }
        }
    }
}