using BusinessEntity.Core.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Contracts
{
    public interface IBusinessEntity
    {
        Guid Id { get; set; }
        string Name { get; set; }

        BusinessEntityTypeEnum BusinessEntityType { get; set; }
    }
}
