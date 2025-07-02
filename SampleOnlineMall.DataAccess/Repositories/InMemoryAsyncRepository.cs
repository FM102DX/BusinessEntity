using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;

namespace SampleOnlineMall.DataAccess.DataAccess
{
    public class InMemoryAsyncRepository<T> : IAsyncRepository<T> where T : BaseEntity, new ()
    {
        
        private List<T> Data { get; set; } = new List<T>();

        public InMemoryAsyncRepository()
        {

        }
        public Task<IEnumerable<T>> GetAllAsync()
        {
            return Task.FromResult((IEnumerable<T>)Data);
        }
        public Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>> filter)
        {
            return Task.FromResult((IEnumerable<T>)Data.Where(filter.Compile()));
        }

        public Task<T> GetByIdOrNullAsync(Guid id)
        {
            T t = Data.FirstOrDefault(x => x.Id == id);
            return Task.FromResult(t);

        }

        public Task<bool> Exists(Guid id)
        {
            return Task.FromResult(GetByIdOrNullAsync(id).Result != null); 
        }

        public Task<CommonOperationResult> AddAsync(T t)
        {
            Data.Add(t);
            return Task.FromResult(CommonOperationResult.SayOk());
        }

        public Task<CommonOperationResult> UpdateAsync(T t)
        {
            var i = Data.IndexOf(t);
            if (i == -1)
            {
                return Task.FromResult(CommonOperationResult.SayFail());
            }
            else
            {
                Data[i]=t;
                return Task.FromResult(CommonOperationResult.SayOk());
            }
        }

        public Task<CommonOperationResult> DeleteAsync(Guid id)
        {
            T t = GetByIdOrNullAsync(id).Result;
            if (t==null)
            {
                return Task.FromResult(CommonOperationResult.SayFail());
            }
            else
            {
                Data.Remove(t);
                return Task.FromResult(CommonOperationResult.SayOk());
            }
        }

        public Task<CommonOperationResult> InitAsync(bool deleteDb = false)
        {
            Data.Clear();
            return Task.FromResult(CommonOperationResult.SayOk());
        }


        public Task<int> GetCountAsync()
        {
            return Task.FromResult(Data.Count);
        }

        public Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> SearchAsync(string text)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetPageAsync(int pageNo, int elementsPerPage)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<T>> GetPageAsync(Expression<Func<T, bool>> filter, int pageNo, int elementsPerPage)
        {
            throw new NotImplementedException();
        }

        public Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestFuncSearch<T> repositoryRequest)
        {
            throw new NotImplementedException();
        }

        public Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestTextSearch repositoryRequest)
        {
            throw new NotImplementedException();
        }

        public Task<CommonOperationResult> ClearAsync()
        {
            throw new NotImplementedException();
        }
    }
}