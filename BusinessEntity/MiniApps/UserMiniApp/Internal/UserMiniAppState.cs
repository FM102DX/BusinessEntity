using BusinessEntity.MiniApps.UserMiniApp.Contracts;

namespace BusinessEntity.MiniApps.UserMiniApp.Internal
{
    internal sealed class UserMiniAppState
    {
        public bool IsLoaded { get; set; }
        public BusinessEntityUser? CurrentUser { get; set; }
    }
}
