using Microsoft.AspNetCore.Components;
using BusinessEntity.Services;
using Microsoft.Extensions.Logging;

namespace BusinessEntity.Components
{
    public partial class RedirectToLogin : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<RedirectToLogin> Logger { get; set; } = default!;

        [Parameter] public string? ReturnUrl { get; set; }

        protected override Task OnInitializedAsync()
        {
            try
            {
                Logger.LogInformation($"Redirecting unauthenticated user to unauthorized page. Return URL: {ReturnUrl}");
                
                // Перенаправляем на страницу unauthorized вместо прямого перехода на логин
                // Это позволяет пользователю увидеть сообщение об ошибке
                Navigation.NavigateTo("/unauthorized", true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during redirect to unauthorized page");
                Navigation.NavigateTo("/unauthorized", true);
            }
            
            return Task.CompletedTask;
        }
    }
}