using BusinessEntity.Core.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories;

/// <summary>
/// Базовый generic-контракт репозитория mini-app хранения данных.
/// </summary>
public interface IAsyncRepository<T> : Contracts.IAsyncRepository<T> where T : IBaseEntity
{
}
