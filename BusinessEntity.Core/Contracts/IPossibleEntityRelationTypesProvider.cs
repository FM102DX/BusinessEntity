using BusinessEntity.Core.Classes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessEntity.Core.Contracts
{
    public interface IPossibleEntityRelationTypesProvider
    {
        IEnumerable<MacroRelationType> GetPossibleRelations();
    }
} 