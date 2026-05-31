using BusinessEntity.MiniApps.TreeMiniApp.Contracts;
using BusinessEntity.MiniApps.TreeMiniApp.Internal;

namespace BusinessEntity.MiniApps.TreeMiniApp.Facade
{
    // Фасад mini-app дерева.
    internal sealed class TreeMiniApp : ITreeMiniApp
    {
        private readonly TreeMiniAppMessageHandler _messageHandler;

        public TreeMiniApp(TreeMiniAppMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
            _messageHandler.EnsureSubscribed();
        }

        public void EnsureInitialized()
        {
            _messageHandler.EnsureSubscribed();
        }
    }
}
