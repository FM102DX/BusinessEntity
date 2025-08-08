using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BusinessEntity.Core.Contracts;

namespace BusinessEntity.Core.Classes
{
    public class Folder : BusinessEntityBase,IBusinessEntity
    {
        public override BusinessEntityTypeEnum BusinessEntityType { get; set; } = BusinessEntityTypeEnum.Folder;
        
    }
}
