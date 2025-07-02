using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;

namespace SampleOnlineMall.WebLogger.Service
{
    public static class MigrationManager
    {
        public static IHost MigrateDatabase(this IHost host)
        {
            using (var scope = host.Services.CreateScope())
            {
                var _logger = scope.ServiceProvider.GetRequiredService<Serilog.ILogger>();
                _logger.Information("P51");

                try
                {
                    var appContext = scope.ServiceProvider.GetRequiredService<DbContext>();
                    
                    _logger.Information("P52");

                        _logger.Information("P53");
                        appContext.Database.Migrate();
                        _logger.Information("P54");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[MigrationManager.MigrateDatabase] ex={ex.Message} innerEx={ex.InnerException}");
                    throw;
                }

            }
            return host;
        }
    }
}
