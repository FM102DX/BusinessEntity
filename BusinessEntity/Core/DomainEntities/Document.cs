using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.DomainEntities
{
    public class Document : BusinessEntityDataBase, IBusinessEntity
    {
        public override BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Document;
    }
}
