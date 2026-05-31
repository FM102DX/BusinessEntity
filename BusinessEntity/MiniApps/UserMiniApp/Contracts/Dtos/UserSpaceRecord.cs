namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Описывает пространство для комбобокса назначения прав.
public sealed class UserSpaceRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}
