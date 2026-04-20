using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;

namespace BusinessEntity.Services
{
    public class SpaceHelper
    {
        private readonly IDataProviderConnector _dataProviderConnector;

        public SpaceHelper(IDataProviderConnector dataProviderConnector)
        {
            _dataProviderConnector = dataProviderConnector;
        }

        /// <summary>
        /// Получает пространство по ID
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetSpaceByIdAsync(Guid spaceId)
        {
            var spaces = await _dataProviderConnector.GetAllAsync<BusinessEntity.Core.Classes.BusinessEntity>(e => e.Id == spaceId && e.EntityType == BusinessEntityTypeEnum.Space);
            return spaces.FirstOrDefault();
        }
    }
}
