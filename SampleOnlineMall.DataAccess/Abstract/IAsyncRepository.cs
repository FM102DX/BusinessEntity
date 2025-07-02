using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;

namespace SampleOnlineMall.DataAccess.Abstract
{
    public interface IAsyncRepository<T> where T : IBaseEntity
    {
        
        //GetAll
        public Task<IEnumerable<T>> GetAllAsync();
        
        public Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestFuncSearch<T> repositoryRequest);

        public Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestTextSearch repositoryRequest);

        //Search
        public Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> filter); //legacy
        public Task<IEnumerable<T>> SearchAsync(string text); //legacy

        //others
        public Task<T> GetByIdOrNullAsync(Guid id);

        public Task<int> GetCountAsync();

        public  Task<bool> Exists(Guid id);

        public Task<CommonOperationResult> AddAsync(T t);

        public Task<CommonOperationResult> UpdateAsync(T t);

        public Task<CommonOperationResult> DeleteAsync(Guid id);

        public Task<CommonOperationResult> InitAsync(bool deleteDb = false);
        public Task<CommonOperationResult> ClearAsync();

    }
}
