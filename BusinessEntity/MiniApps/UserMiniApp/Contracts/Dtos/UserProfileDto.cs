namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// DTO текущего пользовательского профиля для страницы "Профиль".
public sealed class UserProfileDto
{
    public Guid UserId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string AuthentikLogin { get; set; } = string.Empty;
    public string DisplayedName { get; set; } = string.Empty;
}
