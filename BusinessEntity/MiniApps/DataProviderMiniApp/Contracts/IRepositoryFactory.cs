using BusinessEntity.Core.Contracts;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

public interface IRepositoryFactory<T> where T : class, IBaseEntity
{
    IAsyncRepository<T> GetRepository();
} 
