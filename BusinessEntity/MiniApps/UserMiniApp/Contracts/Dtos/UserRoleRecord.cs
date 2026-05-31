namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// DTO роли для административного редактора ролей.
public sealed class UserRoleRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool ViewPublished { get; set; }
    public bool ViewDraft { get; set; }
    public bool EditDraft { get; set; }
    public bool PublishDraft { get; set; }
    public bool AdminItems { get; set; }
    public bool AdminSpace { get; set; }
    public bool GlobalAdmin { get; set; }
    public bool IsSystem { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateLastModified { get; set; }
}
