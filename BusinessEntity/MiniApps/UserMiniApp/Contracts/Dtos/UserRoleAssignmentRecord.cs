namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Отображает назначение роли с именами пространства, субъекта и роли для административной таблицы.
public sealed class UserRoleAssignmentRecord
{
    public Guid Id { get; set; }
    public Guid SpaceId { get; set; }
    public string SpaceName { get; set; } = string.Empty;
    public string Subject { get; set; } = UserRoleAssignmentSubjects.Space;
    public Guid SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string AssignmentType { get; set; } = UserRoleAssignmentTypes.GroupToRole;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
