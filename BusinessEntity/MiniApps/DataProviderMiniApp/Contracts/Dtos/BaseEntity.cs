using System;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

public abstract class BaseEntity : IBaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
} 
