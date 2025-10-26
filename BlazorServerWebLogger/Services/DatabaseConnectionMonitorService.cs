using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorServerWebLogger.Services
{
    public class DatabaseConnectionMonitorService : BackgroundService
    {
        private readonly ILogger<DatabaseConnectionMonitorService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(2); // проверяем каждые 2 минуты

        public DatabaseConnectionMonitorService(
            ILogger<DatabaseConnectionMonitorService> logger, 
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DB-MONITOR] Database connection monitor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckDatabaseConnection();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DB-MONITOR] Error during database connection check");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("[DB-MONITOR] Database connection monitor stopped");
        }

        private async Task CheckDatabaseConnection()
        {
            var connectionString = Environment.GetEnvironmentVariable("IS_DOCKER") == "true"
                ? _configuration.GetConnectionString("DockerConnection")
                : _configuration.GetConnectionString("IisExpressConnection");

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.WriteLine("[DB-MONITOR] ✗ Connection string is empty");
                return;
            }

            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new NpgsqlCommand("SELECT version(), current_database(), current_user", connection);
                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var version = reader.GetString(0).Split(' ')[0] + " " + reader.GetString(0).Split(' ')[1];
                    var database = reader.GetString(1);
                    var user = reader.GetString(2);
                    Console.WriteLine($"[DB-MONITOR] ✓ Postgres connection - SUCCESS. {version}, DB: {database}, User: {user}");
                }
            }
            catch (Exception pgEx)
            {
                Console.WriteLine($"[DB-MONITOR] ✗ Postgres connection FAILED: {pgEx.Message}");
            }
        }
    }
}
