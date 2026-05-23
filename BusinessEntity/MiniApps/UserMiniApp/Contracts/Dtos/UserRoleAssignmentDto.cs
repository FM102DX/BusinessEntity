namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Хранит назначение роли на группу или пользователя в разрезе пространства.
public sealed class UserRoleAssignmentDto
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public string Subject { get; set; } = UserRoleAssignmentSubjects.Space;
    public Guid SubjectId { get; set; }
    public string AssignmentType { get; set; } = UserRoleAssignmentTypes.GroupToRole;
    public Guid RoleId { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
