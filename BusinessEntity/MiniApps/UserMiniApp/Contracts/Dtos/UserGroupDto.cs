namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Хранит группу пользователей UserMiniApp в локальном storage.
public sealed class UserGroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
