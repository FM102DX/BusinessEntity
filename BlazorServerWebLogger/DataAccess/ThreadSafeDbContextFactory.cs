using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly ConcurrentDictionary<string, DbContextPool> _pools = new();
    private readonly int _dbContextLifeTimeMs;

    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options, int dbContextLifeTimeMs = 30000)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
    }

    public DbContextWrap GetDbContext(string poolName = "default")
    {
        var pool = _pools.GetOrAdd(poolName, _ => new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext));
        return pool.GetDbContext();
    }

    private void FreeUpDbContext(WebLoggerDbContext context)
    {
        foreach (var pool in _pools.Values)
        {
            var record = pool.PoolRecords.FirstOrDefault(r => ReferenceEquals(r.Value.Context, context));
            if (record.Value != null)
            {
                record.Value.Busy = false;
                return;
            }
        }
    }
}

public class DbContextPool
{
    private readonly int _dbContextLifeTimeMs;
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    public ConcurrentDictionary<Guid,DbContextPoolRecord> PoolRecords { get; } = new();

    private readonly Timer _cleanupTimer;

    public DbContextPool(int dbContextLifeTimeMs, DbContextOptions<WebLoggerDbContext> options, Action<WebLoggerDbContext> freeUpFunc)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
        _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public DbContextWrap GetDbContext()
    {
        var record = PoolRecords.FirstOrDefault(r => !r.Value.Busy && !r.Value.Expired);
        if (record.Value == null)
        {
            var recordTmp = new DbContextPoolRecord(_dbContextLifeTimeMs)
            {
                TimeStamp = DateTime.Now,
                Busy = true,
                Context = new WebLoggerDbContext(_options)
            };
            PoolRecords.TryAdd(Guid.NewGuid(), recordTmp);
        }
        else
        {
            record.Value.Busy = true;
        }

        return new DbContextWrap(record.Value.Context, _freeUpFunc);
    }

    private void CleanupExpiredRecords(object? state)
    {
        var expiredRecords = PoolRecords
            .Where(record => !record.Value.Busy && record.Value.Expired)
            .ToList();

        foreach (var record in expiredRecords)
        {
            record.Value.Context.Dispose();
            PoolRecords.Remove(record.Key, out _);
        }

        Console.WriteLine($"{expiredRecords.Count} устаревших записей удалено из пула.");
    }
}

public class DbContextPoolRecord
{
    private readonly int _dbContextLifeTimeMs;

    public DbContextPoolRecord(int dbContextLifeTimeMs)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
    }

    public WebLoggerDbContext Context { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Expired => (DateTime.Now - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
    public bool Busy { get; set; }
}

public class DbContextWrap : IDisposable
{
    private readonly WebLoggerDbContext _context;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;

    public DbContextWrap(WebLoggerDbContext context, Action<WebLoggerDbContext> freeUpFunc)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
    }

    public WebLoggerDbContext Context => _context;

    public void Dispose()
    {
        _freeUpFunc(_context);
    }
}
