using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using BlazorServerWebLogger.Service.Logger;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly ConcurrentDictionary<string, DbContextPool> _pools = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly int _dbContextLifeTimeMs;
    private readonly DebugLogger _logger;

    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options, int dbContextLifeTimeMs = 30000, int maxConcurrentThreads = 5)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _semaphore = new SemaphoreSlim(maxConcurrentThreads);
        _logger = new DebugLogger("DBF", true);
    }

    public DbContextWrap GetDbContextWrap(string poolName = "default")
    {
        _semaphore.Wait(); // Ожидание доступа к критической секции
        try
        {
            var pool = _pools.GetOrAdd(poolName, _ => new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext, _logger));
            var wrapTmp = pool.GetDbContext();
            _logger.Write($"[ISSUED] DbContext issued from pool: {wrapTmp.DemoStr}");
            PerformPoolsDump();
            return wrapTmp;
        }
        finally
        {
            _semaphore.Release(); // Освобождаем доступ для других потоков
        }
    }

    private void FreeUpDbContext(WebLoggerDbContext context)
    {
        foreach (var pool in _pools.Values)
        {
            var record = pool.PoolRecords.Values.FirstOrDefault(r => ReferenceEquals(r.Context, context));
            if (record != null)
            {
                lock (record.SyncLock)
                {
                    if (!record.Disposed)
                    {
                        record.Busy = false;
                        _logger.Write($"[RETURNED] DbContext returned to pool: id={record.Id}, busy={record.Busy}");
                        PerformPoolsDump();
                        return;
                    }
                }
            }
        }
    }

    private void PerformPoolsDump()
    {
        _logger.Write($"[POOL STATE] Dumping pool state:");
        foreach (var pool in _pools.Values)
        {
            var freeCount = pool.PoolRecords.Values.Count(r => !r.Busy && !r.Disposed);
            _logger.Write($"  Pool contains {freeCount} free contexts.");
            foreach (var record in pool.PoolRecords.Values)
            {
                _logger.Write($"  id={record.Id,30} busy={record.Busy,7} disposed={record.Disposed,7}");
            }
        }
    }
}

public class DbContextPool
{
    private readonly int _dbContextLifeTimeMs;
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    private readonly DebugLogger _logger;

    public ConcurrentDictionary<Guid, DbContextPoolRecord> PoolRecords { get; } = new();

    private readonly Timer _cleanupTimer;

    public DbContextPool(int dbContextLifeTimeMs, DbContextOptions<WebLoggerDbContext> options, Action<WebLoggerDbContext> freeUpFunc, DebugLogger logger)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
        _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        _logger = logger;
    }

    public DbContextWrap GetDbContext()
    {
        foreach (var record in PoolRecords.Values)
        {
            if (Monitor.TryEnter(record.SyncLock))
            {
                try
                {
                    if (!record.Busy && !record.Expired && !record.Disposed)
                    {
                        record.Busy = true;
                        record.TimeStamp = DateTime.UtcNow;
                        _logger.Write($"[ISSUED] Reusing existing DbContext: id={record.Id}, busy={record.Busy}");
                        return new DbContextWrap(record.Context, _freeUpFunc, record);
                    }
                }
                finally
                {
                    Monitor.Exit(record.SyncLock);
                }
            }
        }

        // Если нет доступных записей, создаём новую
        var id = Guid.NewGuid();
        var newRecord = new DbContextPoolRecord(_dbContextLifeTimeMs, id)
        {
            TimeStamp = DateTime.UtcNow,
            Busy = true,
            Context = new WebLoggerDbContext(_options)
        };

        PoolRecords.TryAdd(id, newRecord);
        _logger.Write($"[CREATED] New DbContext created: id={newRecord.Id}, busy={newRecord.Busy}");
        return new DbContextWrap(newRecord.Context, _freeUpFunc, newRecord);
    }

    private void CleanupExpiredRecords(object? state)
    {
        var expiredRecords = PoolRecords
            .Values
            .Where(record => !record.Busy && record.Expired)
            .ToList();

        foreach (var record in expiredRecords)
        {
            lock (record.SyncLock)
            {
                if (!record.Disposed)
                {
                    record.Context.Dispose();
                    record.Disposed = true;
                    _logger.Write($"[DISPOSED] DbContext disposed: id={record.Id}");
                    PoolRecords.TryRemove(record.Id, out _);
                }
            }
        }
    }
}

public class DbContextPoolRecord
{
    public Guid Id { get; }
    public WebLoggerDbContext Context { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Busy { get; set; }
    public bool Disposed { get; set; }
    public bool Expired => (DateTime.UtcNow - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
    public object SyncLock { get; } = new();

    private readonly int _dbContextLifeTimeMs;

    public DbContextPoolRecord(int dbContextLifeTimeMs, Guid id)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        Id = id;
    }
}

public class DbContextWrap : IDisposable
{
    private readonly WebLoggerDbContext _context;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    private readonly DbContextPoolRecord _record;

    public DbContextWrap(WebLoggerDbContext context, Action<WebLoggerDbContext> freeUpFunc, DbContextPoolRecord record)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
        _record = record ?? throw new ArgumentNullException(nameof(record));
    }

    public WebLoggerDbContext Context => _context;

    public string DemoStr => $"id={_record.Id} busy={_record.Busy}";

    public void Dispose()
    {
        _freeUpFunc(_context);
    }
}
