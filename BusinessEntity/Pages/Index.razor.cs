using System.Collections.ObjectModel;
using BusinessEntity.Contracts;
using BusinessEntity.Data;
using BusinessEntity.Data.Messages;
using BusinessEntity.Data.Services;
using BusinessEntity.Services;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Authorization;
using ReactiveUI;
using SampleOnlineMall.WebLogger.Models;

namespace BusinessEntity.Pages
{
    [Authorize]
    public partial class Index : ComponentBase, IDisposable
    {
        // Сервисы для авторизации
        [Inject] public IAuterlinkAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Index> Logger { get; set; } = default!;

        // Свойства для отображения пользователя
        private string? CurrentUserName { get; set; }
        private string? CurrentUserEmail { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Сначала проверяем, доступен ли сервис авторизации
                var isServiceAvailable = await AuthService.IsServiceAvailableAsync();
                
                if (!isServiceAvailable)
                {
                    Logger.LogError("Auterlink auth service is not available, redirecting to error page");
                    Navigation.NavigateTo("/auth-service-unavailable", true);
                    return;
                }

                // Проверяем, авторизован ли пользователь
                var isAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                
                if (!isAuthenticated)
                {
                    Logger.LogWarning("User is not authenticated, redirecting to unauthorized page");
                    Navigation.NavigateTo("/unauthorized", true);
                    return;
                }

                // Получаем информацию о пользователе
                CurrentUserName = await AuthService.GetUserNameAsync();
                CurrentUserEmail = await AuthService.GetUserEmailAsync();

                Logger.LogInformation($"User {CurrentUserName} accessed KMS Business Entity main page");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading user information or checking auth service availability");
                Navigation.NavigateTo("/auth-service-unavailable", true);
            }
        }

        private async Task SignOut()
        {
            try
            {
                await AuthService.SignOutAsync();
                Navigation.NavigateTo("/auterlink/logout", true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during sign out");
            }
        }

        public void Dispose()
        {
            // Очистка ресурсов при необходимости
        }
    }
}
