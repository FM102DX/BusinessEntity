namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Сериализуемый payload локальной учетной записи пользователя.
public sealed class UserData
{
    public int AuthentikUserPk { get; set; }
    public string AuthentikUserUuid { get; set; } = string.Empty;
    public string AuthentikLogin { get; set; } = string.Empty;
    public string DisplayedName { get; set; } = string.Empty;
    public string ExtId { get; set; } = string.Empty;
}
