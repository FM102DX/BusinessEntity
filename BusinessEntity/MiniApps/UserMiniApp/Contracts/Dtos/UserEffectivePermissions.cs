namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Описывает итоговые права пользователя в одном пространстве после объединения ролей.
public sealed class UserEffectivePermissions
{
    public Guid UserId { get; set; }
    public Guid SpaceId { get; set; }
    public bool IsAnonymous { get; set; }
    public bool CanViewPublished { get; set; }
    public bool CanViewDraft { get; set; }
    public bool CanEditDraft { get; set; }
    public bool CanPublishDraft { get; set; }
    public bool CanAdminItems { get; set; }
    public bool CanAdminSpace { get; set; }
    public bool CanGlobalAdmin { get; set; }

    // Создает deny-result для пользователя и пространства без назначенных прав.
    public static UserEffectivePermissions Deny(Guid userId, Guid spaceId, bool isAnonymous)
    {
        return new UserEffectivePermissions
        {
            UserId = userId,
            SpaceId = spaceId,
            IsAnonymous = isAnonymous
        };
    }
}
