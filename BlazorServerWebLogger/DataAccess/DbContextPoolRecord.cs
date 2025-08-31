using System;
using System.Threading;
using SampleOnlineMall.WebLogger.DataAccess;

public class DbContextPoolRecord
{
    public Guid Id { get; }
    public WebLoggerDbContext Context { get; set; }
    public DateTime TimeStamp { get; set; }
    public bool Busy { get; set; }
    public bool Disposed { get; set; }
    public bool Expired => (DateTime.UtcNow - TimeStamp).TotalMilliseconds > _dbContextLifeTimeMs;
    public object SyncLock { get; } = new();
    public int IssuedCount { get; set; } // Счетчик выдачи DbContext

    private readonly int _dbContextLifeTimeMs;

    public DbContextPoolRecord(int dbContextLifeTimeMs, Guid id)
    {
        _dbContextLifeTimeMs = dbContextLifeTimeMs;
        Id = id;
    }
}
