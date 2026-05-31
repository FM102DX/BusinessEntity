using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// Typed Postgres-репозиторий для технических rich-text чанков.
/// </summary>
public sealed class BusinessEntityDataChunkDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityDataChunkDto>
{
    // Подключает общий ThreadSafeDbContextFactory к rich-text chunk DTO.
    public BusinessEntityDataChunkDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
