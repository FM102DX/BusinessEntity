using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Threading.Tasks;
using SampleOnlineMall.WebLogger.Services;
using SampleOnlineMall.FrontEnd.BlazorServer.Services;
using static SampleOnlineMall.FrontEnd.BlazorServer.Components.ShopItemCollection.ShopItemCollection;

namespace SampleOnlineMall.FrontEnd.BlazorServer.Pages
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Parameter]
        public int? Page { get; set; }

        public int PageToPass 
        { 
            get 
            {
                int page = 0;
                if(Page!=null)
                {
                    page = (int)Page;
                }
                return page;
            } 
        }

        [Inject]
        public IWebLoggerService Logger { get; set; }

        [Inject]
        public IAuterlinkAuthService AuthService { get; set; }

        [Inject]
        public NavigationManager Navigation { get; set; }

        private string? CurrentUserName { get; set; }
        private string? CurrentUserEmail { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                // Проверяем, авторизован ли пользователь
                var isAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                
                if (!isAuthenticated)
                {
                    Logger.Warning("User is not authenticated, redirecting to unauthorized page");
                    Navigation.NavigateTo("/unauthorized", true);
                    return;
                }

                // Получаем информацию о пользователе
                CurrentUserName = await AuthService.GetUserNameAsync();
                CurrentUserEmail = await AuthService.GetUserEmailAsync();

                Logger.Information($"User {CurrentUserName} accessed main page");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error loading user information: {ex.Message}");
                Navigation.NavigateTo("/unauthorized", true);
            }
        }

        protected override void OnInitialized()
        {
            
        }

        public async Task SignOut()
        {
            try
            {
                await AuthService.SignOutAsync();
                Navigation.NavigateTo("/", true); // Перенаправляем сразу на главную страницу
            }
            catch (Exception ex)
            {
                Logger.Error($"Error during sign out: {ex.Message}");
            }
        }
    }
}
