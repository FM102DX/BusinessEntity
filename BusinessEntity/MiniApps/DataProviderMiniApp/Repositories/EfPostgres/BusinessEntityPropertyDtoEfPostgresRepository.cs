using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для технических свойств BusinessEntityDto.
/// </summary>
public sealed class BusinessEntityPropertyDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityPropertyDto>
{
    // Создаёт typed-репозиторий property DTO через общую фабрику DbContext.
    public BusinessEntityPropertyDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
