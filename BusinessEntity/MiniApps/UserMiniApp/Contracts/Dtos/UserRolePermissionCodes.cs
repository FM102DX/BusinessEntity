namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Коды прав роли user mini-app для компактного хранения в строке Permissions.
public static class UserRolePermissionCodes
{
    public const int ViewPublished = 100;
    public const int ViewDraft = 200;
    public const int EditDraft = 300;
    public const int PublishDraft = 400;
    public const int AdminItems = 500;
    public const int AdminSpace = 600;
    public const int GlobalAdmin = 700;
}
