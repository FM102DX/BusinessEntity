using BlazorServerWebLogger.DataAccess.Repository;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using System.Linq.Expressions;

namespace BlazorServerWebLogger.Contracts
{
    public interface IAsyncRepository<T> where T : IBaseEntity
    {

        public Task<DataAccess.Repository.RepositoryResponce<T>> GetAllAsync(Func<T, bool>? filter);

        public Task<T> GetByIdOrNullAsync(Guid id);

        public Task<int> GetCountAsync();

        public Task<bool> ExistsAsync(Guid id);

        public Task<CommonOperationResult> InsertAsync(T t);


        public Task<CommonOperationResult> DeleteOldestReciordsAsync(int leftCount);

        public Task<CommonOperationResult> InitAsync(bool deleteDb = false);

    }
}
