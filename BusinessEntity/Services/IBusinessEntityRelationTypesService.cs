using System.Collections.Generic;

namespace BusinessEntity.Services
{
    public interface IBusinessEntityRelationTypesService
    {
        IEnumerable<string> GetBusinessEntityRelationTypes();
    }
} 