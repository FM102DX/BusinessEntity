using System;

namespace BusinessEntity.Core.Contracts;

public interface IBaseEntity
{
    Guid Id { get; set; }
    DateTime CreatedDate { get; set; }
    DateTime LastModifiedDate { get; set; }
} 