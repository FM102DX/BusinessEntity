using BusinessEntity.Contracts;

namespace BusinessEntity.DataAccess.Repository
{
    public class RepositoryFactory<T> : IRepositoryFactory<T> where T : class, IBaseEntity
    {
        private ThreadSafeDbContextFactory _dbContextFactory;

        public RepositoryFactory(ThreadSafeDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        public IAsyncRepository<T> GetRepository()
        {
            var repository = new EfAsyncRepository<T>(_dbContextFactory);
            return repository;
        }
    }
}
