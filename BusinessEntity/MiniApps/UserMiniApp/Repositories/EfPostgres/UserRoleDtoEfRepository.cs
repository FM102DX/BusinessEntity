using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

// EF/Postgres repository для DTO ролей user mini-app.
public sealed class UserRoleDtoEfRepository : UserMiniAppEfRepositoryBase<UserRoleDto>
{
    // Создает repository ролей поверх общих DbContextOptions user mini-app.
    public UserRoleDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
