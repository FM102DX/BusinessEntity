using BusinessEntity.DataAccess.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Repositories.EfPostgres;

/// <summary>
/// Базовая EF/Postgres-реализация generic-репозитория mini-app хранения данных.
/// </summary>
public abstract class EfPostgresAsyncRepositoryBase<T> : BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.IAsyncRepository<T> where T : class, IBaseEntity
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
        await using var context = _dbContextFactory.CreateDbContext();
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

    // Читает страницу записей с явным order/skip/take без доменной специфики.
    public async Task<IReadOnlyList<T>> GetPageAsync<TKey>(
        Expression<Func<T, bool>>? filter,
        Expression<Func<T, TKey>> orderBy,
        bool descending = false,
        int skip = 0,
        int? take = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orderBy);

        await using var context = _dbContextFactory.CreateDbContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<T> query = context.Set<T>();
        if (filter != null)
        {
            query = query.Where(filter);
        }

        query = descending
            ? query.OrderByDescending(orderBy)
            : query.OrderBy(orderBy);

        if (skip > 0)
        {
            query = query.Skip(skip);
        }

        if (take.HasValue)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(ct);
    }

    /// <summary>
    /// Возвращает запись по идентификатору.
    /// </summary>
    // Читает одну запись текущего DTO-типа по id.
    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    /// <summary>
    /// Проверяет существование записи по идентификатору.
    /// </summary>
    // Проверяет наличие записи в таблице по id.
    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await context.Set<T>().AnyAsync(e => e.Id == id, ct);
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

        await using var context = _dbContextFactory.CreateDbContext();
        await context.Set<T>().AddAsync(entity, ct);
        await context.SaveChangesAsync(ct);
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

        await using var context = _dbContextFactory.CreateDbContext();
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Удаляет запись по идентификатору.
    /// </summary>
    // Находит DTO-запись по id и удаляет её из БД.
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var set = context.Set<T>();
        var entity = await set.FindAsync(new object?[] { id }, ct);
        if (entity == null)
        {
            return;
        }

        set.Remove(entity);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Возвращает количество записей указанного типа.
    /// </summary>
    // Возвращает количество строк для текущего DTO-типа.
    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        return await context.Set<T>().CountAsync(ct);
    }

    // Возвращает количество строк текущего DTO-типа по optional-фильтру.
    public async Task<int> GetCountAsync(Expression<Func<T, bool>>? filter, CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        IQueryable<T> query = context.Set<T>();
        if (filter != null)
        {
            query = query.Where(filter);
        }

        return await query.CountAsync(ct);
    }

    /// <summary>
    /// Полностью очищает таблицу текущего типа.
    /// </summary>
    // Удаляет все записи текущего DTO-типа из таблицы.
    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var set = context.Set<T>();
        set.RemoveRange(set);
        await context.SaveChangesAsync(ct);
    }
}
