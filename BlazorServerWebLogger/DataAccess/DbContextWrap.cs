using System;
using System.Threading;
using SampleOnlineMall.WebLogger.DataAccess;

public class DbContextWrap : IDisposable
{
    private readonly WebLoggerDbContext _context;
    private readonly Action<DbContextPoolRecord> _releaseFunc;
    private readonly DbContextPoolRecord _record;
    private int _disposed;

    public DbContextWrap(WebLoggerDbContext context, Action<DbContextPoolRecord> releaseFunc, DbContextPoolRecord record)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _releaseFunc = releaseFunc ?? throw new ArgumentNullException(nameof(releaseFunc));
        _record = record ?? throw new ArgumentNullException(nameof(record));
    }

    public WebLoggerDbContext Context => _context;

    public string DemoStr => $"id={_record.Id} busy={_record.Busy} issued={_record.IssuedCount}";

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _releaseFunc(_record);
    }
}
