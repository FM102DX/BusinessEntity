using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using BlazorServerWebLogger.Contracts;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using Radzen;

namespace BlazorServerWebLogger.DataAccess.Repository
{
    public class EfAsyncRepository<T> : IAsyncRepository<T> where T : class, IBaseEntity
    {
        private readonly ThreadSafeDbContextFactory _dbContextFactory;

        public EfAsyncRepository(ThreadSafeDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<RepositoryResponce<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, int? count = null)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
                IQueryable<T> query = context.Set<T>();

                // Применяем фильтр, если он задан
                if (filter != null)
                {
                    query = query.Where(filter);
                }

                // Применяем ограничение на количество записей, если оно задано
                if (count != null)
                {
                    query = query.OrderByDescending(x => EF.Property<DateTime>(x, "Timestamp")) // Сортируем по убыванию Timestamp
                        .Take(count.Value);
                }

                // Выполняем запрос и возвращаем результат
                var result = await query.ToListAsync();
                return new RepositoryResponce<T> { Items = result };
            }
        }

        public async Task<T?> GetByIdOrNullAsync(Guid id)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
                return await context.Set<T>().FirstOrDefaultAsync(entity => entity.Id == id);
            }
        }

        public async Task<int> GetCountAsync()
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
                return await context.Set<T>().CountAsync();
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
                return await context.Set<T>().AnyAsync(entity => entity.Id == id);
            }
        }

        public async Task<CommonOperationResult> InsertAsync(T entity)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context=contextWrp.Context;
                await context.Set<T>().AddAsync(entity);
                await context.SaveChangesAsync();
                return new CommonOperationResult { Success = true, Message = "Entity inserted successfully." };
            }
        }

        public async Task<CommonOperationResult> DeleteOldestRecordsAsync(int leftCount)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
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
        }

        public async Task<CommonOperationResult> InitAsync(bool deleteDb = false)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap())
            {
                var context = contextWrp.Context;
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
        }

        Task<CommonOperationResult> IAsyncRepository<T>.DeleteOldestReciordsAsync(int leftCount)
        {
            throw new NotImplementedException();
        }
    }
}
