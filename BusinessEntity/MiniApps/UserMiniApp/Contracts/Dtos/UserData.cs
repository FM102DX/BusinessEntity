namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Сериализуемый payload локальной учетной записи пользователя.
public sealed class UserData
{
    public string DisplayedName { get; set; } = string.Empty;
    public string ExtId { get; set; } = string.Empty;
}
