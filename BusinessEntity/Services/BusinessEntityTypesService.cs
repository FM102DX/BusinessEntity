using BusinessEntity.Core.Classes;
using System.Collections.Generic;
using System.Linq;

namespace BusinessEntity.Services
{
    public class BusinessEntityTypesService : IBusinessEntityTypesService
    {
        public IEnumerable<string> GetBusinessEntityTypes()
        {
            // Возвращаем только Page и Folder из enum
            return new List<string>
            {
                BusinessEntityTypeEnum.Page.ToString(),
                BusinessEntityTypeEnum.Folder.ToString()
            };
        }
    }
}
