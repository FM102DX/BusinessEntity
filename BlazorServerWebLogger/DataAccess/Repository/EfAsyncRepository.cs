using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using BlazorServerWebLogger.Contracts;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;

namespace BlazorServerWebLogger.DataAccess.Repository
{
    public class EfAsyncRepository<T> : IAsyncRepository<T> where T : class, IBaseEntity
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;

        public EfAsyncRepository(ThreadSafeDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<RepositoryResponce<T>> GetAllAsync(Func<T, bool>? filter = null)
        {
            using var context = _dbContextFactory.GetDbContext();

            IQueryable<T> query = context.Set<T>();

            if (filter != null)
            {
                query = query.Where(filter).AsQueryable();
            }

            var result = await query.ToListAsync();
            return new RepositoryResponce<T>() { Items = result };
        }

        public async Task<T?> GetByIdOrNullAsync(Guid id)
        {
            using var context = _dbContextFactory.GetDbContext();
            return await context.Set<T>().FirstOrDefaultAsync(entity => entity.Id == id);
        }

        public async Task<int> GetCountAsync()
        {
            using var context = _dbContextFactory.GetDbContext();
            return await context.Set<T>().CountAsync();
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            using var context = _dbContextFactory.GetDbContext();
            return await context.Set<T>().AnyAsync(entity => entity.Id == id);
        }

        public async Task<CommonOperationResult> InsertAsync(T entity)
        {
            using var context = _dbContextFactory.GetDbContext();

            await context.Set<T>().AddAsync(entity);
            await context.SaveChangesAsync();

            return new CommonOperationResult { Success = true, Message = "Entity inserted successfully." };
        }

        public async Task<CommonOperationResult> DeleteOldestRecordsAsync(int leftCount)
        {
            using var context = _dbContextFactory.GetDbContext();

            var dbSet = context.Set<T>();
            var totalCount = await dbSet.CountAsync();

            if (totalCount <= leftCount)
            {
                return new CommonOperationResult
                {
                    Success = false,
                    Message = "No records to delete."
                };
            }

            var toDelete = await dbSet
                .OrderBy(entity => entity.Timestamp)
                .Take(totalCount - leftCount)
                .ToListAsync();

            dbSet.RemoveRange(toDelete);
            await context.SaveChangesAsync();

            return new CommonOperationResult
            {
                Success = true,
                Message = $"{toDelete.Count} oldest records deleted."
            };
        }

        public async Task<CommonOperationResult> InitAsync(bool deleteDb = false)
        {
            using var context = _dbContextFactory.GetDbContext();

            if (deleteDb)
            {
                await context.Database.EnsureDeletedAsync();
            }

            await context.Database.EnsureCreatedAsync();

            return new CommonOperationResult
            {
                Success = true,
                Message = "Database initialized successfully."
            };
        }

        Task<CommonOperationResult> IAsyncRepository<T>.DeleteOldestReciordsAsync(int leftCount)
        {
            throw new NotImplementedException();
        }
    }
}
