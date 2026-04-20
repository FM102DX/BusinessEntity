using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Facade
{
    /// <summary>
    /// Фасад mini-app хранения данных.
    /// Отвечает только за инициализацию bus-подписок.
    /// </summary>
    internal sealed class DataProviderMiniApp : IDataProviderMiniApp
    {
        private readonly DataProviderMessageHandler _messageHandler;

        /// <summary>
        /// Инициализирует фасад mini-app и активирует message handler.
        /// </summary>
        // Сохраняет handler и сразу гарантирует запуск подписок mini-app.
        public DataProviderMiniApp(
            DataProviderMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
            _messageHandler.EnsureSubscribed();
        }

        // Даёт внешнему коду явную точку для ленивой инициализации mini-app.
        public void EnsureInitialized()
        {
            _messageHandler.EnsureSubscribed();
        }
    }
}
