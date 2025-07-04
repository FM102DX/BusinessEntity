using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.DataAccess.Classes;

/// <summary>
/// DbContext, используемый репозиториями BusinessEntity. Конфигурация таблиц настраивается в вызывающем коде.
/// </summary>
public class KmsBusinessEntityDbContext : DbContext
{
    public KmsBusinessEntityDbContext(DbContextOptions<KmsBusinessEntityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация сущностей выполняется в AddDbContext или через IEntityTypeConfiguration.
        // Оставляем метод пустым, т.к. конкретная схема БД находится вне ответственности DataAccess.
    }
} 