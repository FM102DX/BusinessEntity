using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.UserMiniApp.Internal;

namespace BusinessEntity.MiniApps.UserMiniApp.Facade
{
    internal sealed class UserMiniApp : IUserMiniApp
    {
        private readonly UserMiniAppService _userMiniAppService;

        public UserMiniApp(
            UserMiniAppService userMiniAppService,
            UserMiniAppMessageHandler messageHandler)
        {
            _userMiniAppService = userMiniAppService;
            messageHandler.EnsureSubscribed();
        }

        public Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return _userMiniAppService.GetCurrentUserAsync(cancellationToken);
        }
    }
}
