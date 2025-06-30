using System.Collections.Generic;

namespace BusinessEntity.Services
{
    public interface IBusinessEntityTypesService
    {
        IEnumerable<string> GetBusinessEntityTypes();
    }
}
