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
    public DbSet<BusinessEntityDataChunkDto> BusinessEntityDataChunks => Set<BusinessEntityDataChunkDto>();
    public DbSet<BusinessEntityPropertyDto> BusinessEntityProperties => Set<BusinessEntityPropertyDto>();
    public DbSet<BusinessEntityDataPropertyDto> BusinessEntityDataProperties => Set<BusinessEntityDataPropertyDto>();
    public DbSet<BusinessEntityDataChunkPropertyDto> BusinessEntityDataChunkProperties => Set<BusinessEntityDataChunkPropertyDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessEntityDto>().ToTable("BusinessEntities");
        modelBuilder.Entity<BusinessEntityRelationDto>().ToTable("BusinessEntityRelations");
        modelBuilder.Entity<BusinessEntityDataDto>().ToTable("BusinessEntityDataItems");
        modelBuilder.Entity<BusinessEntityDataDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkDto>().ToTable("BusinessEntityDataChunks");
        modelBuilder.Entity<BusinessEntityDataChunkDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkDto>().Property(x => x.PlainText).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkDto>().Property(x => x.HtmlCache).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkDto>().Property(x => x.Checksum).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityPropertyDto>().ToTable("BusinessEntityProperties");
        modelBuilder.Entity<BusinessEntityPropertyDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityPropertyDto>().Property(x => x.Metadata).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityPropertyDto>().HasIndex(x => x.ParentEntityId);
        modelBuilder.Entity<BusinessEntityPropertyDto>().HasIndex(x => new { x.ParentEntityId, x.PropertyType });
        modelBuilder.Entity<BusinessEntityDataPropertyDto>().ToTable("BusinessEntityDataProperties");
        modelBuilder.Entity<BusinessEntityDataPropertyDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataPropertyDto>().Property(x => x.Metadata).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataPropertyDto>().HasIndex(x => x.ParentEntityId);
        modelBuilder.Entity<BusinessEntityDataPropertyDto>().HasIndex(x => new { x.ParentEntityId, x.PropertyType });
        modelBuilder.Entity<BusinessEntityDataChunkPropertyDto>().ToTable("BusinessEntityDataChunkProperties");
        modelBuilder.Entity<BusinessEntityDataChunkPropertyDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkPropertyDto>().Property(x => x.Metadata).HasColumnType("text");
        modelBuilder.Entity<BusinessEntityDataChunkPropertyDto>().HasIndex(x => x.ParentEntityId);
        modelBuilder.Entity<BusinessEntityDataChunkPropertyDto>().HasIndex(x => new { x.ParentEntityId, x.PropertyType });

        // Явно фиксируем имена таблиц, чтобы shared Postgres-база не зависела от EF-конвенций.
    }
}
