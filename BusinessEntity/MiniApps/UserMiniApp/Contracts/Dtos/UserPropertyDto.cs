namespace BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;

// Техническое пользовательское свойство, привязанное к локальному UserDto.
public sealed class UserPropertyDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateLastModified { get; set; } = DateTime.UtcNow;
    public Guid ParentEntityId { get; set; }
    public int PropertyType { get; set; }
    public string Data { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty;
}
