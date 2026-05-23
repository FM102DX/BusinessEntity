namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Передает изменяемые поля группы пользователей из административного UI.
public sealed class UserGroupSaveRequest
{
    public string Name { get; set; } = string.Empty;
}
