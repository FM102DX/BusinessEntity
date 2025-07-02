using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Classes
{
    public enum BusinessEntityRelationTypeEnum
    {
        StoredInFolder = 100,
        RelatesTo = 200,
        IsStructuralPartOf = 300,

        //например, фолдер содержит фолдер или энтити
        Contains = 1000,

        Undefined = 9999
    }
}
