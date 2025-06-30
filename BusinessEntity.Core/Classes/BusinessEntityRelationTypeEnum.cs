using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public enum BusinessEntityRelationTypeEnum
    {
        //например, фолдер содержит фолдер или энтити
        LogicallyContains = 1000,

        Undefined = 9999
    }
}
