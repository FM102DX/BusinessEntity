using BusinessEntity.Core.Contracts;
using BusinessEntity.DataAccess.Contracts;
using BusinessEntity.DataAccess.Infrastructure;

namespace BusinessEntity.DataAccess.Repositories;

public class RepositoryFactory<T> : IRepositoryFactory<T> where T : class, IBaseEntity
{
    private readonly ThreadSafeDbContextFactory _dbContextFactory;

    public RepositoryFactory(ThreadSafeDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public IAsyncRepository<T> GetRepository()
    {
        return new EfAsyncRepository<T>(_dbContextFactory);
    }
} 