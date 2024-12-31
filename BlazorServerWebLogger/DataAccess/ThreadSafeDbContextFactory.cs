using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BlazorServerWebLogger.Service.Logger;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly ConcurrentDictionary<string, DbContextPool> _pools = new();
    private readonly int _dbContextLifeTimeMs;
    private readonly object _locker = new object();
    private readonly DebugLogger _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5);

    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options, int dbContextLifeTimeMs = 30000)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _logger=new DebugLogger("DBF",true);
    }

    public DbContextWrap GetDbContextWrap(string poolName = "default")
    {
        _semaphore.Wait(); // Синхронно ожидаем доступ к критической секции
        try
        {
            var pool = _pools.GetOrAdd(poolName, _ => new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext,_logger));
            var wrapTmp = pool.GetDbContext();
            PerformPoolsDump(state: null); // Выполняем логирование или другую работу
            return wrapTmp;
        }
        finally
        {
            _semaphore.Release(); // Освобождаем доступ для следующего потока
        }
    }
    private void FreeUpDbContext(WebLoggerDbContext context)
    {
        foreach (var pool in _pools.Values)
        {
            var record = pool.PoolRecords.FirstOrDefault(r => ReferenceEquals(r.Value.Context, context));
            if (record.Value != null)
            {
                record.Value.Busy = false;
                _logger.Write($"returning id={record.Value.Id,30} busy={record.Value.Busy}");
                PerformPoolsDump(null);
                return;
            }
        }
    }

    private void PerformPoolsDump(object? state)
    {
        foreach (var pool in _pools.Values)
        {
            // Пример действия: выводим количество свободных контекстов
            var freeCount = pool.PoolRecords.Count(r => !r.Value.Busy);
            _logger.Write($"");
            _logger.Write($"***********");
            _logger.Write($"Пул содержит {freeCount} свободных контекстов.");
            foreach (var _rec in pool.PoolRecords)
            {
                _logger.Write($"id={_rec.Value.Id,30} busy={_rec.Value.Busy,7} disposed={_rec.Value.Disposed,7} ");
            }
            _logger.Write($"");
        }
    }
}

public class DbContextPool
{
    private readonly int _dbContextLifeTimeMs;
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    private readonly DebugLogger _logger;
    public ConcurrentDictionary<Guid,DbContextPoolRecord> PoolRecords { get; } = new();

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
        DbContextWrap wrapTmp;
        var record = PoolRecords.FirstOrDefault(r => !r.Value.Busy && !r.Value.Expired);
        if (record.Value == null)
        {
            var id = Guid.NewGuid();
            var recordTmp = new DbContextPoolRecord(_dbContextLifeTimeMs,id)
            {
                TimeStamp = DateTime.Now,
                Busy = true,
                Context = new WebLoggerDbContext(_options)
            };
            
            PoolRecords.TryAdd(id, recordTmp);
            wrapTmp = new DbContextWrap(recordTmp.Context, _freeUpFunc, recordTmp);
            _logger.Write($"Giving away new wrap {wrapTmp.DemoStr}");
            return wrapTmp;
        }
        record.Value.Busy = true;
        wrapTmp = new DbContextWrap(record.Value.Context, _freeUpFunc, record.Value);
        _logger.Write($"Giving away existing wrap {wrapTmp.DemoStr}");
        return wrapTmp;
    }

    private void CleanupExpiredRecords(object? state)
    {
        var expiredRecords = PoolRecords
            .Where(record => !record.Value.Busy && record.Value.Expired)
            .ToList();

        foreach (var record in expiredRecords)
        {
            record.Value.Context.Dispose();
            record.Value.Disposed=true;
            _logger.Write($"Disposing record id={record.Value.Id} busy={record.Value.Busy}");
            PoolRecords.Remove(record.Key, out _);
        }
    }
}

public class DbContextPoolRecord
{
    public Guid Id { get; private set; }
    private readonly int _dbContextLifeTimeMs;

    public DbContextPoolRecord(int dbContextLifeTimeMs, Guid id)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        Id = id;
    }

    public WebLoggerDbContext Context { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Expired => (DateTime.Now - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
    public bool Busy { get; set; }
    public bool Disposed { get; set; }
}

public class DbContextWrap : IDisposable
{
    private readonly WebLoggerDbContext _context;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    private readonly DbContextPoolRecord _rec;

    public DbContextWrap(WebLoggerDbContext context, Action<WebLoggerDbContext> freeUpFunc, DbContextPoolRecord rec)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
        _rec= rec;
    }

    public WebLoggerDbContext Context => _context;

    public string DemoStr => $"id={_rec.Id} busy={_rec.Busy}";

    public void Dispose()
    {
        _freeUpFunc(_context);
    }
}
