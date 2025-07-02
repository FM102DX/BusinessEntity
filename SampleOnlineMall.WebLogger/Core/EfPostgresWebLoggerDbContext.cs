using Microsoft.EntityFrameworkCore;
using SampleOnlineMall.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampleOnlineMall.Core
{ 

    public class EfPostgresWebLoggerDbContext : DbContext
    {
        private Microsoft.Extensions.Configuration.ConfigurationManager _confManager;

        public EfPostgresWebLoggerDbContext(Microsoft.Extensions.Configuration.ConfigurationManager confManager)
        {
            _confManager = confManager;
            //Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // optionsBuilder.UseNpgsql("Host=31.31.201.152:5432; Database=assortment; Username=postgres; password=123");
                optionsBuilder.UseNpgsql(_confManager.GetConnectionString("PostgreConnection"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<WebLoggerMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
            });
            modelBuilder.Entity<WebLoggerSettings>(entity =>
            {
                entity.HasKey(e => e.Id);
            });


            //  modelBuilder.Entity<Employee>();
            // modelBuilder.Entity<Role>();

            base.OnModelCreating(modelBuilder);
        }


    }
}
