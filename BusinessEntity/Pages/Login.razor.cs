using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;

namespace BusinessEntity.Pages
{
    [AllowAnonymous]
    public partial class Login : ComponentBase
    {
        [Parameter] 
        [SupplyParameterFromQuery] 
        public string? ReturnUrl { get; set; }

        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Login> Logger { get; set; } = default!;

        private LoginModel loginModel = new();
        private bool isLoading = false;
        private string? ErrorMessage;

        protected override void OnInitialized()
        {
            if (string.IsNullOrEmpty(ReturnUrl))
            {
                ReturnUrl = "/";
            }
        }

        private void HandleLogin()
        {
            try
            {
                isLoading = true;
                ErrorMessage = null;
                StateHasChanged();

                if (string.IsNullOrWhiteSpace(loginModel.Username))
                {
                    ErrorMessage = "Имя пользователя обязательно для заполнения";
                    return;
                }

                // Перенаправляем на контроллер для обработки логина
                var loginUrl = $"/auterlink/processlogin?username={Uri.EscapeDataString(loginModel.Username)}&email={Uri.EscapeDataString(loginModel.Email ?? "")}&returnUrl={Uri.EscapeDataString(ReturnUrl)}";
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during login attempt");
                ErrorMessage = "Произошла ошибка при входе в систему. Попробуйте еще раз.";
            }
            finally
            {
                isLoading = false;
                StateHasChanged();
            }
        }

        public class LoginModel
        {
            public string Username { get; set; } = string.Empty;
            public string? Email { get; set; }
        }
    }
} 