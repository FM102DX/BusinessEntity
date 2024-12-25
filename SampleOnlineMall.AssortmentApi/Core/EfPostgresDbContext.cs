using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Appilcation;
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
        private Serilog.ILogger _logger;
        private Action<string> _loggerAction;
        private SampleOnlineMallAssortmentApiApp _app;

        public EfPostgresDbContext(ConfigurationManager confManager, SampleOnlineMallAssortmentApiApp app, Serilog.ILogger logger )
        {
            _app = app;
            _confManager = confManager;
            _logger = logger;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            _logger.Information($"EfPostgresDbContext_OnConfiguring");

            if (!optionsBuilder.IsConfigured)
            {
                // optionsBuilder.UseNpgsql("Host=31.31.201.152:5432; Database=assortment; Username=postgres; password=123");
                var cnnStr = !_app.IsDocker
                    ? _confManager.GetConnectionString("IisExpressConnection")
                    : _confManager.GetConnectionString("DockerConnection");

                optionsBuilder.UseNpgsql(cnnStr);

                _logger.Information($"EfPostgresDbContext: _app.IsDocker ={_app.IsDocker} using cnnstr = {cnnStr}");

                _loggerAction = _logger.Information;
                optionsBuilder.LogTo(_loggerAction);
            }
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
