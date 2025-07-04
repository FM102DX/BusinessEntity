using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.Core.Contracts;
using BusinessEntity.DataAccess.Classes;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.DataAccess.Repositories;

public class EfAsyncRepository<T> : IAsyncRepository<T> where T : class, IBaseEntity
{
    private readonly ThreadSafeDbContextFactory _dbContextFactory;

    public EfAsyncRepository(ThreadSafeDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap("rp_read");
        var ctx = contextWrap.Context;
        ctx.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        IQueryable<T> query = ctx.Set<T>();
        if (filter != null) query = query.Where(filter);
        if (take.HasValue)
        {
            query = query.OrderByDescending(e => EF.Property<DateTime>(e, nameof(IBaseEntity.CreatedDate))).Take(take.Value);
        }
        var list = await query.ToListAsync(ct);
        return list;
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_getbyid");
        return await wrap.Context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_exists");
        wrap.Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await wrap.Context.Set<T>().AnyAsync(e => e.Id == id, ct);
    }

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_insert");
        await wrap.Context.Set<T>().AddAsync(entity, ct);
        await wrap.Context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_update");
        wrap.Context.Entry(entity).State = EntityState.Modified;
        await wrap.Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_delete");
        var set = wrap.Context.Set<T>();
        var item = await set.FindAsync(new object?[] { id }, ct);
        if (item != null)
        {
            set.Remove(item);
            await wrap.Context.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_count");
        wrap.Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await wrap.Context.Set<T>().CountAsync(ct);
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        using var wrap = _dbContextFactory.GetDbContextWrap("rp_delete_all");
        var set = wrap.Context.Set<T>();
        set.RemoveRange(set);
        await wrap.Context.SaveChangesAsync(ct);
    }

    // Дополнительные вспомогательные методы из старой версии сохраняем, чтобы не ломать вызовы.
    public Task<CommonOperationResult> InitAsync(bool deleteDb = false) => Task.FromResult(CommonOperationResult.Ok());
    public Task<CommonOperationResult> DeleteNOldestRecordsAsync(int toDeleteCount) => Task.FromResult(CommonOperationResult.Ok());
} 