namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Локальная учетная запись пользователя приложения.
public sealed class UserDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateLastModified { get; set; } = DateTime.UtcNow;
}
