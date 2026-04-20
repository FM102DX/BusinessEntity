using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для DTO базовых бизнес-сущностей.
/// </summary>
public sealed class BusinessEntityDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityDto>
{
    /// <summary>
    /// Передает фабрику DbContext в базовый EF-репозиторий.
    /// </summary>
    // Создаёт typed-репозиторий для BusinessEntityDto.
    public BusinessEntityDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
