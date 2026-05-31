namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Представляет группу пользователей с количеством назначенных участников для административного UI.
public sealed class UserGroupRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
