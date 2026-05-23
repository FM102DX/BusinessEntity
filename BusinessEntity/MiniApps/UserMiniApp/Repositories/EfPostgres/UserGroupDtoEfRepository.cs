using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

// EF/Postgres repository для групп пользователей UserMiniApp.
public sealed class UserGroupDtoEfRepository : UserMiniAppEfRepositoryBase<UserGroupDto>
{
    // Передает DbContextOptions базовому repository.
    public UserGroupDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
