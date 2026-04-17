using BusinessEntity.MiniApps.UserMiniApp.Contracts;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    internal sealed class UserMiniAppService
    {
        private readonly UserMiniAppState _state;
        private readonly BusinessEntityUserFactory _userFactory;

        public UserMiniAppService(UserMiniAppState state, BusinessEntityUserFactory userFactory)
        {
            _state = state;
            _userFactory = userFactory;
        }

        public async Task<BusinessEntityUser?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (_state.IsLoaded)
            {
                return _state.CurrentUser;
            }

            _state.CurrentUser = await _userFactory.CreateAsync(cancellationToken);
            _state.IsLoaded = true;
            return _state.CurrentUser;
        }
    }
}
