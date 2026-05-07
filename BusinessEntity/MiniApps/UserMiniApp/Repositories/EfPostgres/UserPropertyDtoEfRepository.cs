using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

public sealed class UserPropertyDtoEfRepository : UserMiniAppEfRepositoryBase<UserPropertyDto>
{
    public UserPropertyDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
