using BusinessEntity.DataAccess.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// Базовая EF/Postgres-реализация generic-репозитория mini-app хранения данных.
/// </summary>
public abstract class EfPostgresAsyncRepositoryBase<T> : BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Repositories.IAsyncRepository<T> where T : class, IBaseEntity
{
    private readonly ThreadSafeDbContextFactory _dbContextFactory;

    /// <summary>
    /// Сохраняет фабрику потокобезопасных DbContext для дальнейших CRUD-операций.
    /// </summary>
    // Сохраняет фабрику DbContext для всех дальнейших EF-операций.
    protected EfPostgresAsyncRepositoryBase(ThreadSafeDbContextFactory dbContextFactory)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    }

    /// <summary>
    /// Возвращает набор записей с optional-фильтром и ограничением количества.
    /// </summary>
    // Читает список записей из таблицы текущего DTO-типа.
    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_read");
        var context = contextWrap.Context;
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<T> query = context.Set<T>();
        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (take.HasValue)
        {
            query = query.OrderByDescending(e => EF.Property<DateTime>(e, nameof(IBaseEntity.CreatedDate))).Take(take.Value);
        }

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Возвращает запись по идентификатору.
    /// </summary>
    // Читает одну запись текущего DTO-типа по id.
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_get");
        return await contextWrap.Context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <summary>
    /// Проверяет существование записи по идентификатору.
    /// </summary>
    // Проверяет наличие записи в таблице по id.
    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_exists");
        contextWrap.Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await contextWrap.Context.Set<T>().AnyAsync(e => e.Id == id, ct);
    }

    /// <summary>
    /// Добавляет новую запись в хранилище.
    /// </summary>
    // Добавляет новую DTO-запись и сохраняет изменения в БД.
    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_add");
        await contextWrap.Context.Set<T>().AddAsync(entity, ct);
        await contextWrap.Context.SaveChangesAsync(ct);
        return entity;
    }

    /// <summary>
    /// Обновляет существующую запись в хранилище.
    /// </summary>
    // Помечает DTO-запись как изменённую и сохраняет изменения.
    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        if (entity == null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_upd");
        contextWrap.Context.Entry(entity).State = EntityState.Modified;
        await contextWrap.Context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Удаляет запись по идентификатору.
    /// </summary>
    // Находит DTO-запись по id и удаляет её из БД.
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_del");
        var set = contextWrap.Context.Set<T>();
        var entity = await set.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return;
        }

        set.Remove(entity);
        await contextWrap.Context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Возвращает количество записей указанного типа.
    /// </summary>
    // Возвращает количество строк для текущего DTO-типа.
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_cnt");
        contextWrap.Context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await contextWrap.Context.Set<T>().CountAsync(ct);
    }

    /// <summary>
    /// Полностью очищает таблицу текущего типа.
    /// </summary>
    // Удаляет все записи текущего DTO-типа из таблицы.
    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        using var contextWrap = _dbContextFactory.GetDbContextWrap($"dpm_{typeof(T).Name}_clr");
        var set = contextWrap.Context.Set<T>();
        set.RemoveRange(set);
        await contextWrap.Context.SaveChangesAsync(ct);
    }
}
