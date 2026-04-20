using Microsoft.EntityFrameworkCore;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.DataAccess.Classes;

/// <summary>
/// DbContext, используемый репозиториями BusinessEntity. Конфигурация таблиц настраивается в вызывающем коде.
/// </summary>
public class KmsBusinessEntityDbContext : DbContext
{
    public KmsBusinessEntityDbContext(DbContextOptions<KmsBusinessEntityDbContext> options) : base(options)
    {
    }

    public DbSet<BusinessEntityDto> BusinessEntities => Set<BusinessEntityDto>();
    public DbSet<BusinessEntityRelationDto> BusinessEntityRelations => Set<BusinessEntityRelationDto>();
    public DbSet<BusinessEntityPropertyDto> BusinessEntityProperties => Set<BusinessEntityPropertyDto>();
    public DbSet<BusinessEntityDataDto> BusinessEntityDataItems => Set<BusinessEntityDataDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessEntityDto>();
        modelBuilder.Entity<BusinessEntityRelationDto>();
        modelBuilder.Entity<BusinessEntityPropertyDto>();
        modelBuilder.Entity<BusinessEntityDataDto>();

        // Конфигурация таблиц остается конвенционной, т.к. mini-app пока использует базовую схему DTO без кастомных маппингов.
    }
} 
