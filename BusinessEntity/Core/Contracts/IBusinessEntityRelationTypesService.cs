using System.Collections.Generic;

namespace BusinessEntity.Contracts
{
    public interface IBusinessEntityRelationTypesService
    {
        IEnumerable<string> GetBusinessEntityRelationTypes();
    }
} 