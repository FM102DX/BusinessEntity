using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Storage;

// Собственный DbContext user mini-app. Использует ту же физическую БД, что и остальные mini-app.
public sealed class UserMiniAppDbContext : DbContext
{
    public UserMiniAppDbContext(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }

    public DbSet<UserDto> Users => Set<UserDto>();
    public DbSet<UserPropertyDto> UserProperties => Set<UserPropertyDto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserDto>().ToTable("Users");
        modelBuilder.Entity<UserDto>().HasKey(x => x.Id);
        modelBuilder.Entity<UserDto>().Property(x => x.ExternalId).HasColumnType("text");
        modelBuilder.Entity<UserDto>().Property(x => x.Payload).HasColumnType("text");
        modelBuilder.Entity<UserDto>().HasIndex(x => x.ExternalId).IsUnique();

        modelBuilder.Entity<UserPropertyDto>().ToTable("UserProperties");
        modelBuilder.Entity<UserPropertyDto>().HasKey(x => x.Id);
        modelBuilder.Entity<UserPropertyDto>().Property(x => x.Data).HasColumnType("text");
        modelBuilder.Entity<UserPropertyDto>().Property(x => x.Metadata).HasColumnType("text");
        modelBuilder.Entity<UserPropertyDto>().HasIndex(x => x.ParentEntityId);
        modelBuilder.Entity<UserPropertyDto>().HasIndex(x => new { x.ParentEntityId, x.PropertyType });
    }
}
