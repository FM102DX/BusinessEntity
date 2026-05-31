namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Передает выбранную группу или пользователя и роль для создания назначения.
public sealed class UserRoleAssignmentSaveRequest
{
    public string Subject { get; set; } = UserRoleAssignmentSubjects.Space;
    public Guid SubjectId { get; set; }
    public string AssignmentType { get; set; } = UserRoleAssignmentTypes.GroupToRole;
    public Guid RoleId { get; set; }
}
