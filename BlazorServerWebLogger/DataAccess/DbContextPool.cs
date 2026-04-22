using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using BlazorServerWebLogger.Service.Logger;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class DbContextPool
{
    private static readonly TimeSpan WaitForFreeContextTimeout = TimeSpan.FromSeconds(5);
    private readonly int _dbContextLifeTimeMs;
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly DebugLogger _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxPoolSize;
    private readonly string _poolName;
    public ConcurrentDictionary<Guid, DbContextPoolRecord> PoolRecords { get; } = new();

    private readonly Timer _cleanupTimer;

    public DbContextPool(string poolName, int dbContextLifeTimeMs, DbContextOptions<WebLoggerDbContext> options, DebugLogger logger, int maxPoolSize)
    {
        _poolName = poolName;
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxPoolSize); // ограничение на количество одновременно выданных контекстов
        _maxPoolSize = maxPoolSize; // строгий лимит на общее число созданных DbContext
        _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public DbContextWrap GetDbContext()
    {
        if (!_semaphore.Wait(WaitForFreeContextTimeout))
        {
            throw new TimeoutException($"Timed out waiting for a free DbContext in pool '{_poolName}'.");
        }

        try
        {
            CleanupExpiredRecordsInternal();

            // сначала пробуем переиспользовать свободный
            var reusable = TryAcquireReusableRecord();
            if (reusable != null)
            {
                Console.WriteLine($"[ISSUED] Reusing existing DbContext: pool={_poolName}, id={reusable.Id}, busy={reusable.Busy}, issued={reusable.IssuedCount}");
                return new DbContextWrap(reusable.Context, ReleaseRecord, reusable);
            }

            // если лимит еще не достигнут, создаем новый контекст
            if (PoolRecords.Count < _maxPoolSize)
            {
                var id = Guid.NewGuid();
                try
                {
                    Console.WriteLine($"[DBPOOL] Создаю новый DbContext, pool={_poolName}, ID: {id}");
                    var newRecord = new DbContextPoolRecord(_dbContextLifeTimeMs, id)
                    {
                        Busy = true,
                        BusySinceUtc = DateTime.UtcNow,
                        Context = new WebLoggerDbContext(_options)
                    };
                    PoolRecords.TryAdd(id, newRecord);
                    Console.WriteLine($"[CREATED] ✓ New DbContext created successfully: pool={_poolName}, id={newRecord.Id}, busy={newRecord.Busy}");
                    return new DbContextWrap(newRecord.Context, ReleaseRecord, newRecord);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DBPOOL-ERROR] ✗ Ошибка создания DbContext: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"[DBPOOL-ERROR] Inner: {ex.InnerException.Message}");
                    }
                    if (ex.Message.Contains("Failed to connect") || ex.Message.Contains("timeout") || ex.Message.Contains("Timeout") || ex.Message.Contains("172.22.0."))
                    {
                        Console.WriteLine($"[DBPOOL-ERROR] Проблемы с подключением к БД.Возможно, IP БД изменился или БД недоступна.");
                    }
                    throw;
                }
            }

            // если слот семафора освободился, но свободный контекст так и не нашли — это ошибка координации пула
            throw new TimeoutException($"No reusable DbContext was found in pool '{_poolName}' after acquiring a slot.");
        }
        catch
        {
            // если произошла ошибка до возврата DbContextWrap — не забываем освободить слот семафора
            _semaphore.Release();
            throw;
        }
    }

    public void ReleaseThread()
    {
        _semaphore.Release(); // освобождаем слот для нового потока
    }

    private DbContextPoolRecord? TryAcquireReusableRecord()
    {
        foreach (var record in PoolRecords.Values)
        {
            if (!Monitor.TryEnter(record.SyncLock))
            {
                continue;
            }

            try
            {
                if (record.Disposed || record.Busy)
                {
                    continue;
                }

                if (record.Expired)
                {
                    DisposeRecordUnsafe(record);
                    continue;
                }

                record.Busy = true;
                record.BusySinceUtc = DateTime.UtcNow;
                record.LastIssuedAtUtc = DateTime.UtcNow;
                record.IssuedCount++;

                return record;
            }
            finally
            {
                Monitor.Exit(record.SyncLock);
            }
        }

        return null;
    }

    private void ReleaseRecord(DbContextPoolRecord record)
    {
        try
        {
            lock (record.SyncLock)
            {
                if (record.Disposed)
                {
                    return;
                }

                // Сбрасываем трекинг и соединение перед возвратом в пул.
                record.Context.ChangeTracker.Clear();
                record.Context.Database.CloseConnection();

                record.Busy = false;
                record.BusySinceUtc = null;

                if (record.Expired)
                {
                    DisposeRecordUnsafe(record);
                    Console.WriteLine($"[DISPOSED] Expired DbContext removed from pool: pool={_poolName}, id={record.Id}");
                    return;
                }

                _logger.Write($"[RETURNED] DbContext returned to pool: pool={_poolName}, id={record.Id}, busy={record.Busy}");
            }
        }
        finally
        {
            ReleaseThread();
        }
    }

    private void CleanupExpiredRecords(object? state)
    {
        CleanupExpiredRecordsInternal();
    }

    private void CleanupExpiredRecordsInternal()
    {
        var expiredRecords = PoolRecords
            .Values
            .Where(record => !record.Busy && record.Expired)
            .ToList();

        foreach (var record in expiredRecords)
        {
            lock (record.SyncLock)
            {
                if (!record.Disposed && !record.Busy && record.Expired)
                {
                    DisposeRecordUnsafe(record);
                    Console.WriteLine($"[DISPOSED] DbContext disposed: pool={_poolName}, id={record.Id}");
                }
            }
        }
    }

    private void DisposeRecordUnsafe(DbContextPoolRecord record)
    {
        record.Context.Dispose();
        record.Disposed = true;
        PoolRecords.TryRemove(record.Id, out _);
    }
}
