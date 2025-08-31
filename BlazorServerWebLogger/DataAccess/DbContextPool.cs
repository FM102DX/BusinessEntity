using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using BlazorServerWebLogger.Service.Logger;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class DbContextPool
{
    private readonly int _dbContextLifeTimeMs;
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly Action<WebLoggerDbContext> _freeUpFunc;
    private readonly DebugLogger _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxPoolSize;
    public ConcurrentDictionary<Guid, DbContextPoolRecord> PoolRecords { get; } = new();

    private readonly Timer _cleanupTimer;

    public DbContextPool(int dbContextLifeTimeMs, DbContextOptions<WebLoggerDbContext> options, Action<WebLoggerDbContext> freeUpFunc, DebugLogger logger, int maxPoolSize)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _freeUpFunc = freeUpFunc ?? throw new ArgumentNullException(nameof(freeUpFunc));
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxPoolSize); // ограничение на количество одновременно выданных контекстов
        _maxPoolSize = maxPoolSize; // строгий лимит на общее число созданных DbContext
        _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public DbContextWrap GetDbContext()
    {
        _semaphore.Wait(); // ждём доступного слота в пуле
        try
        {
            // сначала пробуем переиспользовать свободный
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
                            Console.WriteLine($"[ISSUED] Reusing existing DbContext: id={record.Id}, busy={record.Busy}, issued={record.IssuedCount}");
                            return new DbContextWrap(record.Context, _freeUpFunc, record);
                        }
                    }
                    finally
                    {
                        Monitor.Exit(record.SyncLock);
                    }
                }
            }

            // если нет свободных, проверяем лимит пула
            if (PoolRecords.Count >= _maxPoolSize)
            {
                // достигнут лимит по числу созданных контекстов — ждём освобождения существующего
                while (true)
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
                                    Console.WriteLine($"[ISSUED] Reusing existing DbContext (waited): id={record.Id}, busy={record.Busy}, issued={record.IssuedCount}");
                                    return new DbContextWrap(record.Context, _freeUpFunc, record);
                                }
                            }
                            finally
                            {
                                Monitor.Exit(record.SyncLock);
                            }
                        }
                    }
                    Thread.Sleep(10); // пауза и повтор
                }
            }
            else
            {
                // можно создать новый контекст, лимит ещё не достигнут
                var id = Guid.NewGuid();
                try
                {
                    Console.WriteLine($"[DBPOOL] Создаю новый DbContext, ID: {id}");
                    var newRecord = new DbContextPoolRecord(_dbContextLifeTimeMs, id)
                    {
                        TimeStamp = DateTime.UtcNow,
                        Busy = true,
                        Context = new WebLoggerDbContext(_options)
                    };
                    PoolRecords.TryAdd(id, newRecord);
                    Console.WriteLine($"[CREATED] ✓ New DbContext created successfully: id={newRecord.Id}, busy={newRecord.Busy}");
                    return new DbContextWrap(newRecord.Context, _freeUpFunc, newRecord);
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
                    Console.WriteLine($"[DISPOSED] DbContext disposed: id={record.Id}");
                    PoolRecords.TryRemove(record.Id, out _);
                }
            }
        }
    }
}
