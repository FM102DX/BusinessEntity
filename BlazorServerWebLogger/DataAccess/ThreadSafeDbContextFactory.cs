using System;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.WebLogger.DataAccess;

public class ThreadSafeDbContextFactory
{
    private readonly DbContextOptions<WebLoggerDbContext> _options;
    private readonly ThreadLocal<WebLoggerDbContext> _context = new(() => null);

    public ThreadSafeDbContextFactory(DbContextOptions<WebLoggerDbContext> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Получает или создаёт потокобезопасный экземпляр DbContext.
    /// </summary>
    public WebLoggerDbContext GetDbContext()
    {
        if (_context.Value == null)
        {
            _context.Value = new WebLoggerDbContext(_options);
        }

        return _context.Value;
    }

    /// <summary>
    /// Освобождает текущий потоковый экземпляр DbContext.
    /// </summary>
    public void DisposeDbContext()
    {
        if (_context.Value != null)
        {
            _context.Value.Dispose();
            _context.Value = null;
        }
    }
}