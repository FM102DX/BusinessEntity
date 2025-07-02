using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.Classes
{
    public class BusinessEntity : BusinessEntityBase, IBusinessEntity
    {
        public BusinessEntityTypeEnum EntityType { get; set; } = BusinessEntityTypeEnum.Undefined;
    }
} 