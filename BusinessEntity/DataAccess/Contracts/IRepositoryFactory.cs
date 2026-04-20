using BusinessEntity.Core.Contracts;

namespace BusinessEntity.DataAccess.Contracts;

public interface IRepositoryFactory<T> where T : class, IBaseEntity
{
    IAsyncRepository<T> GetRepository();
} 