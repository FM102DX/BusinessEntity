using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для технических свойств BusinessEntityDataChunkDto.
/// </summary>
public sealed class BusinessEntityDataChunkPropertyDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityDataChunkPropertyDto>
{
    // Создаёт typed-репозиторий property DTO через общую фабрику DbContext.
    public BusinessEntityDataChunkPropertyDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
