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
        }

        public DbSet<LogEntryDbStorable> LogEntries { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            Console.WriteLine("P_WebLoggerDbContext_ctor_3");
            // Конфигурация таблицы LogEntries
            modelBuilder.Entity<LogEntryDbStorable>(entity =>
            {
                entity.HasKey(e => e.Id); // Устанавливаем первичный ключ
                entity.Property(e => e.Timestamp).IsRequired(); // Поле Timestamp обязательно
                entity.Property(e => e.MessageType).IsRequired();
                entity.Property(e => e.ServiceCode).IsRequired();
                entity.Property(e => e.Message).IsRequired();
            });

            modelBuilder.Entity<AppSettingsDbStorable>(entity =>
            {
                entity.HasKey(e => e.Id); // Устанавливаем первичный ключ
                entity.Property(e => e.Timestamp).IsRequired(); // Поле Timestamp обязательно
                entity.Property(e => e.SettingsDomain).IsRequired(); 
                entity.Property(e => e.SettingsJsonData).IsRequired(); 

            });

            Console.WriteLine("P_WebLoggerDbContext_ctor_4");
        }
    }
}

