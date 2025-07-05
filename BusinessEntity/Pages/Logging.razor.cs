using Microsoft.AspNetCore.Components;
using BusinessEntity.Contracts;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Pages
{
    public partial class Logging : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Logging> Logger { get; set; } = default!;
        [Inject] public IWebLoggerService WebLogger { get; set; } = default!;

        private string LogMessage { get; set; } = string.Empty;
        private string StatusMessage { get; set; } = string.Empty;
        private bool IsLoading { get; set; } = false;
        private bool IsError { get; set; } = false;

        // Computed properties for UI
        private string AlertClass => IsError ? "alert-danger" : "alert-success";
        private string StatusLabel => IsError ? "Ошибка:" : "Успех:";

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var isAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                
                if (!isAuthenticated)
                {
                    Logger.LogInformation("Anonymous user accessed Logging page - redirecting to login");
                    RedirectToLogin();
                }
                else
                {
                    var userName = await AuthService.GetUserNameAsync();
                    Logger.LogInformation($"User {userName} accessed Logging page");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during initialization of Logging page");
                SetStatusMessage("Ошибка при инициализации страницы", true);
            }
        }

        private async Task SendLogMessage()
        {
            if (string.IsNullOrWhiteSpace(LogMessage))
            {
                SetStatusMessage("Введите сообщение для логгирования", true);
                return;
            }

            IsLoading = true;
            StateHasChanged();

            try
            {
                var userName = await AuthService.GetUserNameAsync();
                var fullMessage = $"[{userName}] {LogMessage}";
                
                // Отправляем сообщение в веб-логгер
                await WebLogger.Information(fullMessage);
                
                // Логгируем в консольный логгер
                Logger.LogInformation($"Message sent to web logger: {fullMessage}");
                
                SetStatusMessage($"Сообщение успешно отправлено в логгер: \"{LogMessage}\"", false);
                
                // Очищаем поле после успешной отправки
                LogMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error sending message to web logger: {LogMessage}");
                SetStatusMessage($"Ошибка при отправке сообщения: {ex.Message}", true);
            }
            finally
            {
                IsLoading = false;
                StateHasChanged();
            }
        }

        private void ClearMessage()
        {
            LogMessage = string.Empty;
            StatusMessage = string.Empty;
            IsError = false;
            StateHasChanged();
        }

        private void RedirectToLogin()
        {
            try
            {
                var loginUrl = AuthService.GetLoginUrl("/logging");
                Logger.LogInformation($"Redirecting user to Authentic login url={loginUrl}");
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error redirecting to login from Logging page");
                SetStatusMessage("Ошибка при перенаправлении на страницу входа", true);
            }
        }

        private void SetStatusMessage(string message, bool isError)
        {
            StatusMessage = message;
            IsError = isError;
            StateHasChanged();
        }
    }
} 