using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;
using BusinessEntity.DataAccess.Repositories;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Хранит фактические репозитории mini-app и выдаёт их по типу записи.
    /// </summary>
    internal sealed class DataProviderState
    {
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntity.Core.Classes.BusinessEntity> _businessEntityRepository = new InMemoryRepository<BusinessEntity.Core.Classes.BusinessEntity>();
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<Relation> _relationRepository = new InMemoryRepository<Relation>();
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntityData> _businessEntityDataRepository = new InMemoryRepository<BusinessEntityData>();
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntityDto> _businessEntityDtoRepository;
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntityDataDto> _businessEntityDataDtoRepository;
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntityRelationDto> _businessEntityRelationDtoRepository;
        private readonly BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<BusinessEntityPropertyDto> _businessEntityPropertyDtoRepository;

        /// <summary>
        /// Получает готовые EF/Postgres-репозитории DTO и сохраняет их во внутреннем состоянии mini-app.
        /// </summary>
        public DataProviderState(
            BusinessEntityDtoEfPostgresRepository businessEntityDtoRepository,
            BusinessEntityDataDtoEfPostgresRepository businessEntityDataDtoRepository,
            BusinessEntityRelationDtoEfPostgresRepository businessEntityRelationDtoRepository,
            BusinessEntityPropertyDtoEfPostgresRepository businessEntityPropertyDtoRepository)
        {
            _businessEntityDtoRepository = businessEntityDtoRepository;
            _businessEntityDataDtoRepository = businessEntityDataDtoRepository;
            _businessEntityRelationDtoRepository = businessEntityRelationDtoRepository;
            _businessEntityPropertyDtoRepository = businessEntityPropertyDtoRepository;
        }

        /// <summary>
        /// Возвращает внутренний репозиторий для поддерживаемого типа.
        /// </summary>
        public BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T> GetRepository<T>() where T : class, IBaseEntity
        {
            if (typeof(T) == typeof(BusinessEntity.Core.Classes.BusinessEntity))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityRepository;
            }

            if (typeof(T) == typeof(Relation))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_relationRepository;
            }

            if (typeof(T) == typeof(BusinessEntityData))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityDataRepository;
            }

            if (typeof(T) == typeof(BusinessEntityDto))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityDtoRepository;
            }

            if (typeof(T) == typeof(BusinessEntityDataDto))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityDataDtoRepository;
            }

            if (typeof(T) == typeof(BusinessEntityRelationDto))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityRelationDtoRepository;
            }

            if (typeof(T) == typeof(BusinessEntityPropertyDto))
            {
                return (BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T>)_businessEntityPropertyDtoRepository;
            }

            throw new NotSupportedException($"DataProviderMiniApp does not support repository type '{typeof(T).FullName}'.");
        }
    }
}
