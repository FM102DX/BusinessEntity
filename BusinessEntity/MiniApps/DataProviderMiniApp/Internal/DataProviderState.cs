using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Хранит фактические репозитории mini-app и выдаёт их по типу записи.
    /// </summary>
    internal sealed class DataProviderState
    {
        public IAsyncRepository<BusinessEntityDto> BusinessEntityRepository { get; }
        public IAsyncRepository<BusinessEntityDataDto> BusinessEntityDataRepository { get; }
        public IAsyncRepository<BusinessEntityRelationDto> BusinessEntityRelationRepository { get; }

        /// <summary>
        /// Получает готовые EF/Postgres-репозитории DTO и сохраняет их во внутреннем состоянии mini-app.
        /// </summary>
        // Принимает конкретные репозитории и раскладывает их по typed-свойствам.
        public DataProviderState(
            BusinessEntityDtoEfPostgresRepository businessEntityDtoRepository,
            BusinessEntityDataDtoEfPostgresRepository businessEntityDataDtoRepository,
            BusinessEntityRelationDtoEfPostgresRepository businessEntityRelationDtoRepository)
        {
            BusinessEntityRepository = businessEntityDtoRepository;
            BusinessEntityDataRepository = businessEntityDataDtoRepository;
            BusinessEntityRelationRepository = businessEntityRelationDtoRepository;
        }
    }
}
