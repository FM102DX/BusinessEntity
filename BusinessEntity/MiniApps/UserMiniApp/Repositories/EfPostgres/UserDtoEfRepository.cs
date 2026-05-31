using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

public sealed class UserDtoEfRepository : UserMiniAppEfRepositoryBase<UserDto>
{
    public UserDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
