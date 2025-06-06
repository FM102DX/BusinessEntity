using Microsoft.AspNetCore.Components;

namespace BusinessEntity.Pages
{
    public partial class AuthServiceUnavailable : ComponentBase
    {
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<AuthServiceUnavailable> Logger { get; set; } = default!;

        private void RetryConnection()
        {
            Logger.LogInformation("User requested retry connection to auth service");
            // Перенаправляем на главную страницу для повторной попытки авторизации
            Navigation.NavigateTo("/", true);
        }

        private void ContactSupport()
        {
            Logger.LogInformation("User requested to contact support for auth service issue");
            // Здесь можно добавить логику для связи с поддержкой
            // Например, открыть модальное окно с контактами или перенаправить на страницу поддержки
            
            // Пока что просто логируем событие
            // В реальном приложении здесь может быть:
            // - Открытие модального окна с контактами
            // - Отправка email в службу поддержки
            // - Перенаправление на внешнюю систему тикетов
        }
    }
}