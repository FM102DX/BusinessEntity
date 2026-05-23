namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Хранит связь пользователя UserMiniApp с пользовательской группой.
public sealed class UserGroupMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
