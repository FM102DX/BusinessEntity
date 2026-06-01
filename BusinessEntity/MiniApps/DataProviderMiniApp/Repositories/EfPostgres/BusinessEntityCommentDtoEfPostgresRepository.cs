using BusinessEntity.DataAccess.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// EF/Postgres-репозиторий для комментариев, привязанных к BusinessEntity.
/// </summary>
public sealed class BusinessEntityCommentDtoEfPostgresRepository : EfPostgresAsyncRepositoryBase<BusinessEntityCommentDto>
{
    /// <summary>
    /// Передает фабрику DbContext в базовый EF-репозиторий.
    /// </summary>
    // Создает typed-репозиторий для BusinessEntityCommentDto.
    public BusinessEntityCommentDtoEfPostgresRepository(ThreadSafeDbContextFactory dbContextFactory) : base(dbContextFactory)
    {
    }
}
