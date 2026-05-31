using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

// EF/Postgres repository для назначений ролей UserMiniApp.
public sealed class UserRoleAssignmentDtoEfRepository : UserMiniAppEfRepositoryBase<UserRoleAssignmentDto>
{
    // Передает DbContextOptions базовому repository назначений ролей.
    public UserRoleAssignmentDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
