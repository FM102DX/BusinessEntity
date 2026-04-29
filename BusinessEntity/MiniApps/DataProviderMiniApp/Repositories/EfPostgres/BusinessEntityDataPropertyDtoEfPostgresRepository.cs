using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для технических свойств BusinessEntityDataDto.
/// </summary>
public sealed class BusinessEntityDataPropertyDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityDataPropertyDto>
{
    // Создаёт typed-репозиторий property DTO через общую фабрику DbContext.
    public BusinessEntityDataPropertyDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
