using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntityBase : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        
        public virtual BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Undefined;

    }
}
