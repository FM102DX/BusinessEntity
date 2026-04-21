using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.DomainEntities
{
    public class Folder : BusinessEntityDataBase,IBusinessEntity
    {
        public override BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Folder;
        
    }
}
