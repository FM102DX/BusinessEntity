namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Описывает готовое решение UserMiniApp по доступу текущего пользователя к контенту.
public sealed class UserContentAccessDecision
{
    public bool IsOwner { get; set; }
    public bool IsAccessAdmin { get; set; }
    public bool CanViewDraft { get; set; }
    public bool CanViewPublished { get; set; }
    public bool CanRead { get; set; }
    public bool CanEditDraft { get; set; }
    public bool CanPublishDraft { get; set; }
    public bool CanChangeCommonFlag { get; set; }
    public bool CanViewSpaceContainer { get; set; }
}
