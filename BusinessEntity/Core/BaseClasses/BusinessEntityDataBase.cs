using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntityDataBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
        public string Name { get; set; } = string.Empty;
        
        public virtual BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
        
        public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    }
}
