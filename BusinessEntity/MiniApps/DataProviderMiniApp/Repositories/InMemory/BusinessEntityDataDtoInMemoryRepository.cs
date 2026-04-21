using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.InMemory;

/// <summary>
/// In-memory репозиторий для DTO сериализованных данных бизнес-объектов.
/// </summary>
public sealed class BusinessEntityDataDtoInMemoryRepository : InMemoryAsyncRepositoryBase<BusinessEntityDataDto>
{
}
