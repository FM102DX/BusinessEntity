using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.DataAccess.Infrastructure;

// NOTE: код скопирован без изменений логики. Логирование остаётся через DebugLogger.
public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<KmsBusinessEntityDbContext> _options;
    private readonly ConcurrentDictionary<string, DbContextPool> _pools = new();
    private readonly int _dbContextLifeTimeMs;
    private readonly DebugLogger _logger;

    public ThreadSafeDbContextFactory(DbContextOptions<KmsBusinessEntityDbContext> options, int dbContextLifeTimeMs = 30000)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _logger = new DebugLogger("DBF", true, "", false);
    }

    public DbContextWrap GetDbContextWrap(string rawKey, int maxPoolSize = 5)
    {
        try
        {
            var processedKey = ProcessPoolKey(rawKey);
            var pool = _pools.GetOrAdd(processedKey, _ => new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext, _logger, maxPoolSize));
            var contextWrap = pool.GetDbContext();
            LogPoolsState();
            return contextWrap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при получении DbContextWrap {ex.Message} {ex.InnerException}");
            throw;
        }
    }

    private string ProcessPoolKey(string rawKey)
    {
        var cleanedKey = Regex.Replace(rawKey ?? "default", "[^a-zA-Z0-9_]", "");
        if (cleanedKey.Length > 20) cleanedKey = cleanedKey.Substring(0, 20);
        return cleanedKey.ToLower();
    }

    private void FreeUpDbContext(KmsBusinessEntityDbContext context)
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
                        pool.ReleaseThread();
                        LogPoolsState();
                        return;
                    }
                }
            }
        }
    }

    private void LogPoolsState()
    {
        _logger.Write("");
        _logger.Write($"[POOLS DUMP] Pools total={_pools.Count}");
        foreach (var pool in _pools)
        {
            var poolName = pool.Key;
            var poolValue = pool.Value;
            var freeCount = poolValue.PoolRecords.Values.Count(r => !r.Busy);
            _logger.Write($"[POOL STATE] Pool name={poolName} DbContexts {freeCount}/{poolValue.PoolRecords.Count} free ");
            foreach (var record in poolValue.PoolRecords.Values)
            {
                _logger.Write($"[Record] id={record.Id,30} busy={record.Busy,7} disposed={record.Disposed} issued={record.IssuedCount}");
            }
        }
        _logger.Write("");
    }

    // ----- nested classes -----

    private class DbContextPool
    {
        private readonly int _dbContextLifeTimeMs;
        private readonly DbContextOptions<KmsBusinessEntityDbContext> _options;
        private readonly Action<KmsBusinessEntityDbContext> _freeUpFunc;
        private readonly DebugLogger _logger;
        private readonly SemaphoreSlim _semaphore;
        public ConcurrentDictionary<Guid, DbContextPoolRecord> PoolRecords { get; } = new();
        private readonly Timer _cleanupTimer;

        public DbContextPool(int dbContextLifeTimeMs, DbContextOptions<KmsBusinessEntityDbContext> options, Action<KmsBusinessEntityDbContext> freeUpFunc, DebugLogger logger, int maxPoolSize)
        {
            _dbContextLifeTimeMs = dbContextLifeTimeMs;
            _options = options;
            _freeUpFunc = freeUpFunc;
            _logger = logger;
            _semaphore = new SemaphoreSlim(maxPoolSize);
            _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }

        public DbContextWrap GetDbContext()
        {
            _semaphore.Wait();
            try
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
                                record.IssuedCount++;
                                _logger.Write($"[ISSUED] Reusing existing DbContext: id={record.Id}, busy={record.Busy}, issued={record.IssuedCount}");
                                return new DbContextWrap(record.Context, _freeUpFunc, record);
                            }
                        }
                        finally
                        {
                            Monitor.Exit(record.SyncLock);
                        }
                    }
                }

                var id = Guid.NewGuid();
                var newRecord = new DbContextPoolRecord(_dbContextLifeTimeMs, id)
                {
                    TimeStamp = DateTime.UtcNow,
                    Busy = true,
                    Context = new KmsBusinessEntityDbContext(_options)
                };
                PoolRecords.TryAdd(id, newRecord);
                _logger.Write($"[CREATED] New DbContext created: id={newRecord.Id}, busy={newRecord.Busy}");
                return new DbContextWrap(newRecord.Context, _freeUpFunc, newRecord);
            }
            catch
            {
                _semaphore.Release();
                throw;
            }
        }

        public void ReleaseThread() => _semaphore.Release();

        private void CleanupExpiredRecords(object? state)
        {
            foreach (var record in PoolRecords.Values)
            {
                if (!record.Expired) continue;
                lock (record.SyncLock)
                {
                    if (record.Disposed) continue;
                    record.Disposed = true;
                    record.Context.Dispose();
                    _logger.Write($"[DISPOSED] DbContext disposed: id={record.Id}");
                }
            }
        }
    }

    private class DbContextPoolRecord
    {
        private readonly int _dbContextLifeTimeMs;
        public Guid Id { get; }
        public KmsBusinessEntityDbContext Context { get; set; }
        public DateTime TimeStamp { get; set; }
        public bool Busy { get; set; }
        public bool Disposed { get; set; }
        public bool Expired => (DateTime.UtcNow - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
        public object SyncLock { get; } = new();
        public int IssuedCount { get; set; }
        public DbContextPoolRecord(int dbContextLifeTimeMs, Guid id)
        {
            _dbContextLifeTimeMs = dbContextLifeTimeMs;
            Id = id;
        }
    }

    public class DbContextWrap : IDisposable
    {
        private readonly KmsBusinessEntityDbContext _context;
        private readonly Action<KmsBusinessEntityDbContext> _freeUpFunc;
        private readonly DbContextPoolRecord _record;

        public DbContextWrap(KmsBusinessEntityDbContext context, Action<KmsBusinessEntityDbContext> freeUpFunc, DbContextPoolRecord record)
        {
            _context = context;
            _freeUpFunc = freeUpFunc;
            _record = record;
        }

        public KmsBusinessEntityDbContext Context => _context;
        public string DemoStr => $"id={_record.Id} busy={_record.Busy} issued={_record.IssuedCount}";

        public void Dispose()
        {
            _freeUpFunc(_context);
        }
    }
} 