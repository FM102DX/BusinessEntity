using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для DTO связей между бизнес-объектами.
/// </summary>
public sealed class BusinessEntityRelationDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityRelationDto>
{
    /// <summary>
    /// Передает фабрику DbContext в базовый EF-репозиторий.
    /// </summary>
    public BusinessEntityRelationDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
