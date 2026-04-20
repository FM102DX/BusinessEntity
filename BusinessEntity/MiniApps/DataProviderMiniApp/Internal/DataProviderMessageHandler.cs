using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Централизованно подписывает mini-app на bus-запросы к хранилищу и публикует типизированные ответы.
    /// </summary>
    internal sealed class DataProviderMessageHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly DataProviderService _dataProviderService;
        private readonly ILogger<DataProviderMessageHandler> _logger;
        private readonly List<IDisposable> _subscriptions = new();

        /// <summary>
        /// Получает bus, внутренний сервис mini-app и логгер.
        /// </summary>
        public DataProviderMessageHandler(
            IMessageBus messageBus,
            DataProviderService dataProviderService,
            ILogger<DataProviderMessageHandler> logger)
        {
            _messageBus = messageBus;
            _dataProviderService = dataProviderService;
            _logger = logger;
        }

        /// <summary>
        /// Инициализирует подписки mini-app один раз на текущий scope bus.
        /// </summary>
        public void EnsureSubscribed()
        {
            if (_subscriptions.Count > 0)
            {
                return;
            }

            SubscribeFor<BusinessEntity.Core.Classes.BusinessEntity>();
            SubscribeFor<Relation>();
            SubscribeFor<BusinessEntityData>();
            SubscribeFor<BusinessEntityDto>();
            SubscribeFor<BusinessEntityDataDto>();
            SubscribeFor<BusinessEntityRelationDto>();
            SubscribeFor<BusinessEntityPropertyDto>();

            _logger.LogInformation("DataProviderMiniApp subscribed to storage messages.");
        }

        /// <summary>
        /// Подписывает все supported storage-операции для конкретного типа записи.
        /// </summary>
        private void SubscribeFor<T>() where T : class, IBaseEntity
        {
            _subscriptions.Add(_messageBus.Listen<GetRecordsRequest<T>>().Subscribe(request => _ = HandleGetRecordsAsync(request)));
            _subscriptions.Add(_messageBus.Listen<GetRecordByIdRequest<T>>().Subscribe(request => _ = HandleGetByIdAsync(request)));
            _subscriptions.Add(_messageBus.Listen<RecordExistsRequest<T>>().Subscribe(request => _ = HandleExistsAsync(request)));
            _subscriptions.Add(_messageBus.Listen<AddRecordRequest<T>>().Subscribe(request => _ = HandleAddAsync(request)));
            _subscriptions.Add(_messageBus.Listen<UpdateRecordRequest<T>>().Subscribe(request => _ = HandleUpdateAsync(request)));
            _subscriptions.Add(_messageBus.Listen<DeleteRecordRequest<T>>().Subscribe(request => _ = HandleDeleteAsync(request)));
            _subscriptions.Add(_messageBus.Listen<GetRecordCountRequest<T>>().Subscribe(request => _ = HandleCountAsync(request)));
            _subscriptions.Add(_messageBus.Listen<DeleteAllRecordsRequest<T>>().Subscribe(request => _ = HandleDeleteAllAsync(request)));
        }

        /// <summary>
        /// Обрабатывает запрос списка записей.
        /// </summary>
        private async Task HandleGetRecordsAsync<T>(GetRecordsRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                var records = await _dataProviderService.GetAllAsync(request.Filter, request.Take);
                _messageBus.SendMessage(new GetRecordsResponse<T>(request.RequestId, records));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load records for {RecordType}.", typeof(T).Name);
                _messageBus.SendMessage(new GetRecordsResponse<T>(request.RequestId, Array.Empty<T>(), ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает запрос записи по идентификатору.
        /// </summary>
        private async Task HandleGetByIdAsync<T>(GetRecordByIdRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                var record = await _dataProviderService.GetByIdAsync<T>(request.Id);
                _messageBus.SendMessage(new GetRecordByIdResponse<T>(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load record {RecordId} for {RecordType}.", request.Id, typeof(T).Name);
                _messageBus.SendMessage(new GetRecordByIdResponse<T>(request.RequestId, null, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает запрос проверки существования записи.
        /// </summary>
        private async Task HandleExistsAsync<T>(RecordExistsRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                var exists = await _dataProviderService.ExistsAsync<T>(request.Id);
                _messageBus.SendMessage(new RecordExistsResponse<T>(request.RequestId, exists));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check record {RecordId} for {RecordType}.", request.Id, typeof(T).Name);
                _messageBus.SendMessage(new RecordExistsResponse<T>(request.RequestId, false, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает команду добавления записи.
        /// </summary>
        private async Task HandleAddAsync<T>(AddRecordRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                var record = await _dataProviderService.AddAsync(request.Record);
                _messageBus.SendMessage(new AddRecordResponse<T>(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add record for {RecordType}.", typeof(T).Name);
                _messageBus.SendMessage(new AddRecordResponse<T>(request.RequestId, null, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает команду обновления записи.
        /// </summary>
        private async Task HandleUpdateAsync<T>(UpdateRecordRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                await _dataProviderService.UpdateAsync(request.Record);
                _messageBus.SendMessage(new UpdateRecordResponse<T>(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update record {RecordId} for {RecordType}.", request.Record.Id, typeof(T).Name);
                _messageBus.SendMessage(new UpdateRecordResponse<T>(request.RequestId, false, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает команду удаления записи.
        /// </summary>
        private async Task HandleDeleteAsync<T>(DeleteRecordRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                await _dataProviderService.DeleteAsync<T>(request.Id);
                _messageBus.SendMessage(new DeleteRecordResponse<T>(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete record {RecordId} for {RecordType}.", request.Id, typeof(T).Name);
                _messageBus.SendMessage(new DeleteRecordResponse<T>(request.RequestId, false, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает запрос количества записей.
        /// </summary>
        private async Task HandleCountAsync<T>(GetRecordCountRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                var count = await _dataProviderService.GetCountAsync<T>();
                _messageBus.SendMessage(new GetRecordCountResponse<T>(request.RequestId, count));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to count records for {RecordType}.", typeof(T).Name);
                _messageBus.SendMessage(new GetRecordCountResponse<T>(request.RequestId, 0, ex.Message));
            }
        }

        /// <summary>
        /// Обрабатывает команду полной очистки хранилища.
        /// </summary>
        private async Task HandleDeleteAllAsync<T>(DeleteAllRecordsRequest<T> request) where T : class, IBaseEntity
        {
            try
            {
                await _dataProviderService.DeleteAllAsync<T>();
                _messageBus.SendMessage(new DeleteAllRecordsResponse<T>(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete all records for {RecordType}.", typeof(T).Name);
                _messageBus.SendMessage(new DeleteAllRecordsResponse<T>(request.RequestId, false, ex.Message));
            }
        }

        /// <summary>
        /// Освобождает все bus-подписки mini-app.
        /// </summary>
        public void Dispose()
        {
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
        }
    }
}
