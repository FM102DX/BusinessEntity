namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Передает полный выбранный список пользователей для группы.
public sealed class UserGroupMembershipSaveRequest
{
    public List<Guid> UserIds { get; set; } = new();
}
