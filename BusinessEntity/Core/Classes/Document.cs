using BusinessEntity.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public class Document : BusinessEntityBase, IBusinessEntity
    {
        public override BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Document;
    }
}
