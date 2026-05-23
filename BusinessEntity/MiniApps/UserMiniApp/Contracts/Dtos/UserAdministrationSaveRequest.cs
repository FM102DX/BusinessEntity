namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// DTO команды создания или обновления локального пользователя из административного UI.
public sealed class UserAdministrationSaveRequest
{
    public string ExternalId { get; set; } = string.Empty;
    public string AuthentikLogin { get; set; } = string.Empty;
    public string DisplayedName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
