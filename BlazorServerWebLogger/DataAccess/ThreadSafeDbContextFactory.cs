using System;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

//класс -фабрика дб-контекстов, которая также управляет их жизненным циклом,
//чтобы не допустить слишком частого их создания или слишком большого их количества
public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private Dictionary<string, DbContextPool> _pools = new();
    private int _dbContextLifeTimeMs = 30000;
    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Получает или создаёт потокобезопасный экземпляр DbContext.
    /// </summary>
    public DbContextWrap GetDbContext(string poolName = "1")
    {
        // ищем пул с таким ключом, если нет, создаем
        var pool = _pools[poolName];
        if (pool == null)
        {
            pool = new DbContextPool(_dbContextLifeTimeMs, _options, FreeUpDbContext);
            _pools.Add(poolName, pool);
        }
        return pool.GetDbContext();
    }

    public void FreeUpDbContext(WebLoggerDbContext context)
    {
        var result = _pools
                                        .Values
                                        .SelectMany(x => x.PoolRecords)
                                        .Where(x => ReferenceEquals(x.Context, context))
                                        .FirstOrDefault();
        if (result != null)
        {
            result.Busy = false; // маркер того, что контекст можно назначать другой задаче
        }
    }
}

public class DbContextPool
{
    private int _dbContextLifeTimeMs;
    DbContextOptions<WebLoggerDbContext> _options;
    private Action<WebLoggerDbContext> _freeUpFunc;
    public List<DbContextPoolRecord> PoolRecords = new();
    private Timer _cleanupTimer;
    public DbContextPool(int dbContextLifeTimeMs, DbContextOptions<WebLoggerDbContext> options, Action<WebLoggerDbContext> freeUpFunc)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        _options = options;
        _freeUpFunc = freeUpFunc;
        // Запуск таймера для очистки
        _cleanupTimer = new Timer(CleanupExpiredRecords, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }
    public DbContextWrap GetDbContext()
    {
        var result = PoolRecords.Where(x => !x.Busy).ToList();
        if (result.Count() > 0)
        {
            return result[0].Context;
        }
        else
        {
            //создаем новую запись в этом pool
            var newPoolRec = new DbContextPoolRecord(_dbContextLifeTimeMs)
            {
                TimeStamp = DateTime.Now,
                Busy = true,
                Context = new WebLoggerDbContext(_options)
            };
            PoolRecords.Add(newPoolRec);

            //отдаем дбконтекст
            return newPoolRec.Context;
        }
        return null;
    }
    private void CleanupExpiredRecords(object? state)
    {
        lock (PoolRecords) // Синхронизация доступа к коллекции
        {
            var expiredRecords = PoolRecords
                .Where(record => !record.Busy && record.Expired)
                .ToList();

            foreach (var record in expiredRecords)
            {
                record.Context?.Dispose(); // Освобождаем ресурсы контекста
                PoolRecords.Remove(record); // Удаляем из коллекции
            }

            Console.WriteLine($"{expiredRecords.Count} устаревших записей удалено из пула.");
        }
    }
}
public class DbContextPoolRecord
{
    private int _dbContextLifeTimeMs;
    public DbContextPoolRecord(int dbContextLifeTimeMs)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
    }
    public WebLoggerDbContext Context { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Expired => (DateTime.Now - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
    public bool Busy { get; set; }
}

//это класс отдается наружу и используется в конструкциях using
public class DbContextWrap : IDisposable
{
    private WebLoggerDbContext _context;
    private Action<WebLoggerDbContext> _freeUpFunc;
    public DbContextWrap(WebLoggerDbContext context, Action<WebLoggerDbContext> freeUpFunc)
    {
        _freeUpFunc = freeUpFunc;
        _context = context;
    }
    public WebLoggerDbContext Context { get; set; }

    public void Dispose()
    {

        //здесь надо просто освободить DbContext
        _freeUpFunc(_context);
    }
}

