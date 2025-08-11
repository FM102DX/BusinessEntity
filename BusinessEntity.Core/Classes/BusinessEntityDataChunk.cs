using System;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntityDataChunk : BaseEntity
    {
        public Guid EntityId { get; set; }
        public string Chunk { get; set; } = string.Empty;
    }
}
