namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// DTO команды сохранения роли из административного редактора ролей.
public sealed class UserRoleSaveRequest
{
    public string Name { get; set; } = string.Empty;
    public bool ViewPublished { get; set; }
    public bool ViewDraft { get; set; }
    public bool EditDraft { get; set; }
    public bool PublishDraft { get; set; }
    public bool AdminItems { get; set; }
    public bool AdminSpace { get; set; }
    public bool GlobalAdmin { get; set; }
}
