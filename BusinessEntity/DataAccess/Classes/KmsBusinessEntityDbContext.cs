using Microsoft.EntityFrameworkCore;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.DataAccess.Classes;

/// <summary>
/// DbContext, используемый репозиториями BusinessEntityData. Конфигурация таблиц настраивается в вызывающем коде.
/// </summary>
public class KmsBusinessEntityDbContext : DbContext
{
    public KmsBusinessEntityDbContext(DbContextOptions<KmsBusinessEntityDbContext> options) : base(options)
    {
    }

    public DbSet<BusinessEntityDto> BusinessEntities => Set<BusinessEntityDto>();
    public DbSet<BusinessEntityRelationDto> BusinessEntityRelations => Set<BusinessEntityRelationDto>();
    public DbSet<BusinessEntityDataDto> BusinessEntityDataItems => Set<BusinessEntityDataDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessEntityDto>().ToTable("BusinessEntities");
        modelBuilder.Entity<BusinessEntityRelationDto>().ToTable("BusinessEntityRelations");
        modelBuilder.Entity<BusinessEntityDataDto>().ToTable("BusinessEntityDataItems");

        // Явно фиксируем имена таблиц, чтобы shared Postgres-база не зависела от EF-конвенций.
    }
} 
