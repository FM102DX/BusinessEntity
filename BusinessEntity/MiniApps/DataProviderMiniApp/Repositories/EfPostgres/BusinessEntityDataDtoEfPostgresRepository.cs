using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для DTO сериализованных данных бизнес-объектов.
/// </summary>
public sealed class BusinessEntityDataDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityDataDto>
{
    /// <summary>
    /// Передает фабрику DbContext в базовый EF-репозиторий.
    /// </summary>
    // Создаёт typed-репозиторий для BusinessEntityDataDto.
    public BusinessEntityDataDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
