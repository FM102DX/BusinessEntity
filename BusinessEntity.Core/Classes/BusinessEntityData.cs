using System;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntityData : BaseEntity
    {
        public Guid EntityId { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}
