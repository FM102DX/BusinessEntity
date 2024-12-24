using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleOnlineMall.Core
{ 

    public class EfPostgresDbContext : DbContext
    {
        private Microsoft.Extensions.Configuration.ConfigurationManager _confManager;
        private WebLoggerManager _logger;
        private Action<string> _loggerAction;

        public EfPostgresDbContext(Microsoft.Extensions.Configuration.ConfigurationManager confManager, WebLoggerManager logger)
        {
            _confManager = confManager;
            _logger = logger;
            _logger.Information("context_init_4");
            Database.EnsureCreated();
            _logger.Information("context_init_5");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            _logger.Information("context_init_1");
            if (!optionsBuilder.IsConfigured)
            {
                _logger.Information("context_init_2");
                // optionsBuilder.UseNpgsql("Host=31.31.201.152:5432; Database=assortment; Username=postgres; password=123");
                optionsBuilder.UseNpgsql(_confManager.GetConnectionString("PostgreConnection"));
                _loggerAction = _logger.Information;
                optionsBuilder.LogTo(_loggerAction);
            }
            _logger.Information("context_init_3");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<CommodityItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(250);
            });
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(250);
            });
            

            //  modelBuilder.Entity<Employee>();
            // modelBuilder.Entity<Role>();

            base.OnModelCreating(modelBuilder);
        }


    }
}
