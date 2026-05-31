using BlazorServerWebLogger.DataAccess.Repository;
using BusinessEntity.Service;
using System.Linq.Expressions;

namespace BlazorServerWebLogger.Contracts
{
    public interface IAsyncRepository<T> where T : IBaseEntity
    {

        public Task<DataAccess.Repository.RepositoryResponce<T>> GetAllAsync(Expression<Func<T, bool>>? filter, int? count);

        public Task<T> GetByIdOrNullAsync(Guid id);

        public Task<int> GetCountAsync();

        public Task<bool> ExistsAsync(Guid id);

        public Task<CommonOperationResult> InsertAsync(T t);
        public Task<CommonOperationResult> UpdateAsync(T t);


        public Task<CommonOperationResult> DeleteNOldestRecordsAsync(int toDeleteCount);

        public Task<CommonOperationResult> InitAsync(bool deleteDb = false);
        public Task<CommonOperationResult> DeleteAllAsync();

    }
}
