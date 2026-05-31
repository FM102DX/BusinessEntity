namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Хранимая DTO роли пользователя в UserMiniApp storage.
public sealed class UserRoleDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Permissions { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateLastModified { get; set; } = DateTime.UtcNow;
}
