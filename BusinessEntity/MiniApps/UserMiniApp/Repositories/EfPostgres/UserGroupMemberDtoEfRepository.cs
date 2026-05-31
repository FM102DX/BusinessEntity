using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.UserMiniApp.Storage;
using Microsoft.EntityFrameworkCore;

namespace BusinessEntity.MiniApps.UserMiniApp.Repositories.EfPostgres;

// EF/Postgres repository для связей пользователей и групп UserMiniApp.
public sealed class UserGroupMemberDtoEfRepository : UserMiniAppEfRepositoryBase<UserGroupMemberDto>
{
    // Передает DbContextOptions базовому repository.
    public UserGroupMemberDtoEfRepository(DbContextOptions<UserMiniAppDbContext> options) : base(options)
    {
    }
}
