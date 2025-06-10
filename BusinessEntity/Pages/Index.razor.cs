using System.Collections.ObjectModel;
using BusinessEntity.Contracts;
using BusinessEntity.Data;
using BusinessEntity.Data.Messages;
using BusinessEntity.Data.Services;
using BusinessEntity.Services;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ReactiveUI;
using SampleOnlineMall.WebLogger.Models;

namespace BusinessEntity.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Index> Logger { get; set; } = default!;

        private string? CurrentUserName { get; set; }
        private string? CurrentUserEmail { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var isAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                
                if (isAuthenticated)
                {
                    CurrentUserName = await AuthService.GetUserNameAsync();
                    CurrentUserEmail = await AuthService.GetUserEmailAsync();
                    Logger.LogInformation($"User {CurrentUserName} accessed main page");
                }
                else
                {
                    Logger.LogInformation("Anonymous user accessed main page");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading user information");
            }
        }

        private void RedirectToLogin()
        {
            try
            {
                var loginUrl = AuthService.GetLoginUrl("/");
                Logger.LogInformation($"Redirecting user to Authentic login url={loginUrl}");
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error redirecting to login");
            }
        }

        private Task SignOut()
        {
            try
            {
                Logger.LogInformation($"User {CurrentUserName} is requesting sign out");
                
                // Очищаем локальные данные
                CurrentUserName = null;
                CurrentUserEmail = null;
                
                // Принудительно обновляем компонент
                StateHasChanged();
                
                Logger.LogInformation("Redirecting to sign out page");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during sign out preparation");
            }
            finally
            {
                // Перенаправляем на страницу выхода
                Navigation.NavigateTo("/auth/logout", forceLoad: true);
            }
            
            return Task.CompletedTask;
        }
    }
}
