using System;
using SampleOnlineMall.WebLogger.DataAccess;

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

    public string DemoStr => $"id={_record.Id} busy={_record.Busy} issued={_record.IssuedCount}";

    public void Dispose()
    {
        _freeUpFunc(_context);
    }
}
