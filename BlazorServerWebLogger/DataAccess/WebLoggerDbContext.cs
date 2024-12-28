using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.App;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.DataAccess;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.WebLogger.Models;

using Microsoft.EntityFrameworkCore;

namespace SampleOnlineMall.WebLogger.DataAccess
{
    /// <summary>
    /// Контекст базы данных для хранения логов.
    /// </summary>
    public class WebLoggerDbContext : DbContext
    {
        public WebLoggerDbContext(DbContextOptions<WebLoggerDbContext> options) : base(options)
        {
            // Убедитесь, что база данных и таблицы созданы
            Database.EnsureCreated();
        }

        public DbSet<LogEntryDbStorable> LogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Конфигурация таблицы LogEntries
            modelBuilder.Entity<LogEntryDbStorable>(entity =>
            {
                entity.HasKey(e => e.Id); // Устанавливаем первичный ключ
                entity.Property(e => e.Message).IsRequired(); // Поле Message обязательно
                entity.Property(e => e.Timestamp).IsRequired(); // Поле Timestamp обязательно
                entity.Property(e => e.MessageType).IsRequired(); // Поле MessageType обязательно
                entity.Property(e => e.ServiceCode).IsRequired(); // Поле ServiceCode обязательно
            });
        }
    }
}

