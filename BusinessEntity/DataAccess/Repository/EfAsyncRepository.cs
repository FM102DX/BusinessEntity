using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;
using BusinessEntity.Contracts;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using Radzen;

namespace BusinessEntity.DataAccess.Repository
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
            try
            {
                using (var contextWrp = _dbContextFactory.GetDbContextWrap(rawKey: "rp_read", maxPoolSize: 20))
                {
                    var context = contextWrp.Context;
                    context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                    IQueryable<T> query = context.Set<T>();

                    // Применяем фильтр, если он задан
                    if (filter != null)
                    {
                        query = query.Where(filter);
                    }

                    // Применяем ограничение на количество записей, если оно задано
                    if (count != null)
                    {
                        query = query.OrderByDescending(x => EF.Property<DateTime>(x, "Timestamp"))
                            .Take(count.Value);
                    }

                    // Выполняем запрос и возвращаем результат
                    var result = await query.ToListAsync();
                    return new RepositoryResponce<T> { Items = result };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в GetAllAsync: {ex.Message}");
                throw;
            }
        }

        public async Task<T?> GetByIdOrNullAsync(Guid id)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_getbyid"))
            {
                var context = contextWrp.Context;
                //это трекается
                return await context.Set<T>().FirstOrDefaultAsync(entity => entity.Id == id);
            }
        }

        public async Task<int> GetCountAsync()
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_count"))
            {
                var context = contextWrp.Context;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return await context.Set<T>().CountAsync();
            }
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_exist"))
            {
                var context = contextWrp.Context;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                return await context.Set<T>().AnyAsync(entity => entity.Id == id);
            }
        }

        public async Task<CommonOperationResult> InsertAsync(T entity)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_insert_update"))
            {
                var context = contextWrp.Context;
                await context.Set<T>().AddAsync(entity);
                await context.SaveChangesAsync();
                return new CommonOperationResult { Success = true, Message = "Entity inserted successfully." };
            }
        }

        public async Task<CommonOperationResult> InitAsync(bool deleteDb = false)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_read"))
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

        public async Task<CommonOperationResult> DeleteNOldestRecordsAsync(int toDeleteCount)
        {
            using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_delеte"))
            {
                var context = contextWrp.Context;
                var dbSet = context.Set<T>();

                var toDelete = await dbSet
                    .OrderBy(entity => entity.Timestamp)
                    .Take(toDeleteCount)
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
        public async Task<CommonOperationResult> DeleteAllAsync()
        {
            try
            {
                using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_delete"))
                {
                    var context = contextWrp.Context;
                    var dbSet = context.Set<T>();

                    // Удаление всех записей
                    dbSet.RemoveRange(dbSet);
                    await context.SaveChangesAsync();

                    return CommonOperationResult.SayOk("All records deleted successfully.");
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку и возвращаем отрицательный результат
                Console.WriteLine($"Error during DeleteAllAsync: {ex.Message}");
                return CommonOperationResult.SayFail($"Failed to delete records: {ex.Message}");
            }
        }

        public async Task<CommonOperationResult> UpdateAsync(T entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            try
            {
                using (var contextWrp = _dbContextFactory.GetDbContextWrap("rp_insert_update"))
                {
                    var context = contextWrp.Context;

                    // Добавляем новую сущность и помечаем её как изменённую
                    context.Entry(entity).State = EntityState.Modified;

                    // Сохраняем изменения
                    await context.SaveChangesAsync();
                }

                return new CommonOperationResult
                {
                    Success = true,
                    Message = "Entity updated successfully."
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during UpdateAsync: {ex.Message}");
                return new CommonOperationResult
                {
                    Success = false,
                    Message = $"Update failed: {ex.Message}"
                };
            }
        }

    }
}
