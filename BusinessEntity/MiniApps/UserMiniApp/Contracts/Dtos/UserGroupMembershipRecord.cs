namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Представляет состояние назначения одного пользователя в выбранную группу.
public sealed class UserGroupMembershipRecord
{
    public Guid UserId { get; set; }
    public string DisplayedName { get; set; } = string.Empty;
    public string AuthentikLogin { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public bool IsMember { get; set; }
}
