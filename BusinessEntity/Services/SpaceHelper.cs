using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;

namespace BusinessEntity.Services
{
    public class SpaceHelper
    {
        private readonly BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity> _repository;

        public SpaceHelper(BusinessEntity.Core.Contracts.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity> repository)
        {
            _repository = repository;
        }

        /// <summary>
        /// Получает пространство по ID
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetSpaceByIdAsync(Guid spaceId)
        {
            var spaces = await _repository.GetAllAsync(e => e.Id == spaceId && e.EntityType.ToString() == "Space");
            return spaces.FirstOrDefault();
        }
    }
}
