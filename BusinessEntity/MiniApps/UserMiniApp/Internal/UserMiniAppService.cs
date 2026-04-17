using BusinessEntity.MiniApps.UserMiniApp.Contracts;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    // Инкапсулирует основную логику получения и кэширования текущего пользователя в пределах scope.
    internal sealed class UserMiniAppService
    {
        private readonly UserMiniAppState _state;
        private readonly BusinessEntityUserFactory _userFactory;

        // Получает state mini-app и фабрику, которая умеет собирать пользователя из Authentik principal.
        public UserMiniAppService(UserMiniAppState state, BusinessEntityUserFactory userFactory)
        {
            _state = state;
            _userFactory = userFactory;
        }

        // Возвращает пользователя из state или один раз строит его через factory.
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
