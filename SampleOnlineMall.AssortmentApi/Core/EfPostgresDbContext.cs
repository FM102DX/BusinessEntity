using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using SampleOnlineMall.Core.Appilcation;
using SampleOnlineMall.Core.Managers;
using SampleOnlineMall.Core.Models;
using SampleOnlineMall.Service;
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
        private ConfigurationManager _confManager;
        private SampleOnlineMallAssortmentApiApp _app;
        private GenericAppSettings _genApp;

        public EfPostgresDbContext(ConfigurationManager confManager, 
                                    SampleOnlineMallAssortmentApiApp app,
                                    GenericAppSettings genApp)
        {
            _app = app;
            _genApp = genApp;
            _confManager = confManager;
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var cnnStr = !_genApp.IsDocker
                    ? _confManager.GetConnectionString("IisExpressConnection")
                    : _confManager.GetConnectionString("DockerConnection");
                optionsBuilder.UseNpgsql(cnnStr);
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
