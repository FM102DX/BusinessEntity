using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using BlazorServerWebLogger.Service.Logger;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly ConcurrentDictionary<string, DbContextPool> _pools = new();
    private readonly int _dbContextLifeTimeMs;
    private readonly int _maxPoolSize;
    private readonly DebugLogger _logger;

    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options, int dbContextLifeTimeMs = 30000)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _maxPoolSize = 4; // hard cap of DbContexts in the pool
        _logger = new DebugLogger("DBF", true,"",false);
    }

    public DbContextWrap GetDbContextWrap(string rawKey, int maxPoolSize = 5)
    {
        try
        {
            // Обработка ключа
            var processedKey = ProcessPoolKey(rawKey);
            //Console.WriteLine($"P1");
            // Получаем или создаем пул
            // ignore per-call maxPoolSize; enforce factory-wide cap
            var pool = _pools.GetOrAdd(processedKey, _ => new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext, _logger, _maxPoolSize));
            //Console.WriteLine($"P2");
            // Получаем контекст из пула
            var contextWrap = pool.GetDbContext();
            //Console.WriteLine($"P3");
            // Логируем состояние пулов
            LogPoolsState();
            //Console.WriteLine($"P4");
            return contextWrap;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DBFACTORY-ERROR] Ошибка при получении DbContextWrap: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[DBFACTORY-ERROR] Inner exception: {ex.InnerException.Message}");
            }
            
            // Если это сетевая ошибка, логируем дополнительную информацию
            if (ex.Message.Contains("Failed to connect") || ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"))
            {
                Console.WriteLine($"[DBFACTORY-ERROR] ⚠ NETWORK/TIMEOUT ERROR DETECTED - это может быть связано с изменением IP БД");
                Console.WriteLine($"[DBFACTORY-ERROR] Время ошибки: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            }
            
            throw;
        }
    }

    private string ProcessPoolKey(string rawKey)
    {
        // Unify all calls into a single shared pool
        return "default";
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
                        pool.ReleaseThread(); // Освобождаем слот для нового потока в пуле

                        LogPoolsState(); // Логирование состояния пулов после возврата DbContext
                        return;
                    }
                }
            }
        }
    }

    private void LogPoolsState()
    {
        // _logger.Write("");
        // _logger.Write($"[POOLS DUMP] Pools total={_pools.Count}");
        foreach (var pool in _pools)
        {
            var poolName = pool.Key; // Ключ текущего пула
            var poolValue = pool.Value; // Значение текущего пула

            var freeCount = poolValue.PoolRecords.Values.Count(r => !r.Busy);
            //  _logger.Write($"[POOL STATE] Pool name={poolName} DbContexts {freeCount}/{poolValue.PoolRecords.Count} free ");

            foreach (var record in poolValue.PoolRecords.Values)
            {
                //    _logger.Write($"[Record] id={record.Id,30} busy={record.Busy,7} disposed={record.Disposed} issued={record.IssuedCount}");
            }
        }
        // _logger.Write("");
    }
}
