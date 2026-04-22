using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using ReactiveUI;
using BusinessEntity.Core.Classes;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Централизованно подписывает mini-app на bus-запросы к хранилищу и публикует ответы.
    /// </summary>
    internal sealed class DataProviderMessageHandler : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly IDataProviderCrudService _dataProviderService;
        private readonly ILogger<DataProviderMessageHandler> _logger;
        private readonly IWebLoggerService? _webLogger;
        private readonly List<IDisposable> _subscriptions = new();

        public DataProviderMessageHandler(
            IMessageBus messageBus,
            IDataProviderCrudService dataProviderService,
            ILogger<DataProviderMessageHandler> logger,
            IWebLoggerService? webLogger)
        {
            _messageBus = messageBus;
            _dataProviderService = dataProviderService;
            _logger = logger;
            _webLogger = webLogger;
        }

        // Один раз подписывает mini-app на все входящие bus-сообщения.
        public void EnsureSubscribed()
        {
            if (_subscriptions.Count > 0)
            {
                return;
            }

            SubscribeBusinessEntityMessages();
            SubscribeRelationMessages();
            SubscribeBusinessEntityDataMessages();

            _logger.LogInformation("DataProviderMiniApp subscribed to storage messages.");
        }

        // Подписывает сообщения, которые работают с BusinessEntityData.
        private void SubscribeBusinessEntityMessages()
        {
            _subscriptions.Add(_messageBus.Listen<GetBusinessEntitiesRequest>().Subscribe(request => _ = HandleGetBusinessEntitiesAsync(request)));
            _subscriptions.Add(_messageBus.Listen<GetBusinessEntityByIdRequest>().Subscribe(request => _ = HandleGetBusinessEntityByIdAsync(request)));
            _subscriptions.Add(_messageBus.Listen<AddBusinessEntityRequest>().Subscribe(request => _ = HandleAddBusinessEntityAsync(request)));
            _subscriptions.Add(_messageBus.Listen<UpdateBusinessEntityRequest>().Subscribe(request => _ = HandleUpdateBusinessEntityAsync(request)));
            _subscriptions.Add(_messageBus.Listen<DeleteBusinessEntityRequest>().Subscribe(request => _ = HandleDeleteBusinessEntityAsync(request)));
            _subscriptions.Add(_messageBus.Listen<ClearDataProviderStorageRequest>().Subscribe(request => _ = HandleClearStorageAsync(request)));
        }

        // Подписывает сообщения, которые работают с BusinessEntityRelation.
        private void SubscribeRelationMessages()
        {
            _subscriptions.Add(_messageBus.Listen<GetAllRelationsRequest>().Subscribe(request => _ = HandleGetAllRelationsAsync(request)));
            _subscriptions.Add(_messageBus.Listen<GetRelationsRequest>().Subscribe(request => _ = HandleGetRelationsAsync(request)));
            _subscriptions.Add(_messageBus.Listen<GetRelationByIdRequest>().Subscribe(request => _ = HandleGetRelationByIdAsync(request)));
            _subscriptions.Add(_messageBus.Listen<CreateRelationRequest>().Subscribe(request => _ = HandleCreateRelationAsync(request)));
            _subscriptions.Add(_messageBus.Listen<UpdateRelationRequest>().Subscribe(request => _ = HandleUpdateRelationAsync(request)));
            _subscriptions.Add(_messageBus.Listen<DeleteRelationRequest>().Subscribe(request => _ = HandleDeleteRelationAsync(request)));
        }

        // Подписывает сообщения, которые работают с BusinessEntityData.
        private void SubscribeBusinessEntityDataMessages()
        {
            _subscriptions.Add(_messageBus.Listen<GetBusinessEntityDataRequest>().Subscribe(request => _ = HandleGetDataAsync(request)));
            _subscriptions.Add(_messageBus.Listen<UpdateBusinessEntityDataRequest>().Subscribe(request => _ = HandleUpdateDataAsync(request)));
        }

        // Обрабатывает запрос на чтение всех бизнес-сущностей.
        private async Task HandleGetBusinessEntitiesAsync(GetBusinessEntitiesRequest request)
        {
            try
            {
                var records = await _dataProviderService.GetAllAsync();
                _messageBus.SendMessage(new GetBusinessEntitiesResponse(request.RequestId, records));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load business entities.");
                _messageBus.SendMessage(new GetBusinessEntitiesResponse(request.RequestId, Array.Empty<BusinessEntity.Core.Classes.BusinessEntity>(), ex.Message));
            }
        }

        // Обрабатывает запрос на чтение одной сущности по id.
        private async Task HandleGetBusinessEntityByIdAsync(GetBusinessEntityByIdRequest request)
        {
            try
            {
                var record = await _dataProviderService.GetByIdAsync(request.Id);
                _messageBus.SendMessage(new GetBusinessEntityByIdResponse(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load business entityData {RecordId}.", request.Id);
                _messageBus.SendMessage(new GetBusinessEntityByIdResponse(request.RequestId, null, ex.Message));
            }
        }

        // Обрабатывает запрос на чтение бинарного payload сущности.
        private async Task HandleGetDataAsync(GetBusinessEntityDataRequest request)
        {
            try
            {
                var data = await _dataProviderService.GetDataPayloadAsync(request.BusinessEntityId);
                _messageBus.SendMessage(new GetBusinessEntityDataResponse(request.RequestId, data));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load business entityData data {RecordId}.", request.BusinessEntityId);
                _messageBus.SendMessage(new GetBusinessEntityDataResponse(request.RequestId, null, ex.Message));
            }
        }

        // Обрабатывает команду на создание бизнес-сущности.
        private async Task HandleAddBusinessEntityAsync(AddBusinessEntityRequest request)
        {
            try
            {
                _webLogger?.Information($"[мини-апп:data-provider] [bus:received] [entity:add] Получено AddBusinessEntityRequest requestId={request.RequestId} entityId={request.Record.Id} type={request.Record.EntityType} name='{request.Record.Name}'");
                var record = await _dataProviderService.AddAsync(request.Record);
                _messageBus.SendMessage(new AddBusinessEntityResponse(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add business entityData.");
                _messageBus.SendMessage(new AddBusinessEntityResponse(request.RequestId, null, ex.Message));
            }
        }

        // Обрабатывает команду на обновление бизнес-сущности.
        private async Task HandleUpdateBusinessEntityAsync(UpdateBusinessEntityRequest request)
        {
            try
            {
                await _dataProviderService.UpdateAsync(request.Record);
                _messageBus.SendMessage(new UpdateBusinessEntityResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update business entityData {RecordId}.", request.Record.Id);
                _messageBus.SendMessage(new UpdateBusinessEntityResponse(request.RequestId, false, ex.Message));
            }
        }

        // Обрабатывает команду на удаление бизнес-сущности.
        private async Task HandleDeleteBusinessEntityAsync(DeleteBusinessEntityRequest request)
        {
            try
            {
                await _dataProviderService.DeleteAsync(request.Id);
                _messageBus.SendMessage(new DeleteBusinessEntityResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete business entityData {RecordId}.", request.Id);
                _messageBus.SendMessage(new DeleteBusinessEntityResponse(request.RequestId, false, ex.Message));
            }
        }

        // Обрабатывает debug-команду на полную очистку DTO-хранилища mini-app.
        private async Task HandleClearStorageAsync(ClearDataProviderStorageRequest request)
        {
            try
            {
                await _dataProviderService.ClearAllAsync();
                _messageBus.SendMessage(new ClearDataProviderStorageResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear DataProvider storage.");
                _messageBus.SendMessage(new ClearDataProviderStorageResponse(request.RequestId, false, ex.Message));
            }
        }

        // Обрабатывает команду на сохранение бинарного payload сущности.
        private async Task HandleUpdateDataAsync(UpdateBusinessEntityDataRequest request)
        {
            try
            {
                // _webLogger?.Information($"[мини-апп:data-provider] [bus:received] [entity-data:update] Получено UpdateBusinessEntityDataRequest requestId={request.RequestId} entityId={request.BusinessEntityId} payloadLength={request.Data?.Length ?? 0}");
                await _dataProviderService.UpdateDataPayloadAsync(request.BusinessEntityId, request.Data);
                _messageBus.SendMessage(new UpdateBusinessEntityDataResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update business entityData data {RecordId}.", request.BusinessEntityId);
                _messageBus.SendMessage(new UpdateBusinessEntityDataResponse(request.RequestId, false, ex.Message));
            }
        }

        // Обрабатывает запрос на чтение всех связей.
        private async Task HandleGetAllRelationsAsync(GetAllRelationsRequest request)
        {
            try
            {
                var records = await _dataProviderService.GetAllRelationsAsync();
                _messageBus.SendMessage(new GetAllRelationsResponse(request.RequestId, records));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load relations.");
                _messageBus.SendMessage(new GetAllRelationsResponse(request.RequestId, Array.Empty<BusinessEntityRelation>(), ex.Message));
            }
        }

        // Обрабатывает запрос на чтение связей между двумя сущностями.
        private async Task HandleGetRelationsAsync(GetRelationsRequest request)
        {
            try
            {
                var records = await _dataProviderService.GetRelationsAsync(request.ObjectAId, request.ObjectBId);
                _messageBus.SendMessage(new GetRelationsResponse(request.RequestId, records));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load relations between {ObjectAId} and {ObjectBId}.", request.ObjectAId, request.ObjectBId);
                _messageBus.SendMessage(new GetRelationsResponse(request.RequestId, Array.Empty<BusinessEntityRelation>(), ex.Message));
            }
        }

        // Обрабатывает запрос на чтение одной связи по id.
        private async Task HandleGetRelationByIdAsync(GetRelationByIdRequest request)
        {
            try
            {
                var record = await _dataProviderService.GetRelationByIdAsync(request.Id);
                _messageBus.SendMessage(new GetRelationByIdResponse(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load relation {RecordId}.", request.Id);
                _messageBus.SendMessage(new GetRelationByIdResponse(request.RequestId, null, ex.Message));
            }
        }

        // Обрабатывает команду на создание связи.
        private async Task HandleCreateRelationAsync(CreateRelationRequest request)
        {
            try
            {
                // _webLogger?.Information($"[мини-апп:data-provider] [bus:received] [relation:add] Получено CreateRelationRequest requestId={request.RequestId} relationId={request.Record.Id} objectA={request.Record.ObjectAId} objectB={request.Record.ObjectBId} type={request.Record.RelationType}");
                var record = await _dataProviderService.CreateRelationAsync(request.Record);
                _messageBus.SendMessage(new CreateRelationResponse(request.RequestId, record));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add relation.");
                _messageBus.SendMessage(new CreateRelationResponse(request.RequestId, null, ex.Message));
            }
        }

        // Обрабатывает команду на обновление связи.
        private async Task HandleUpdateRelationAsync(UpdateRelationRequest request)
        {
            try
            {
                await _dataProviderService.UpdateRelationAsync(request.Record);
                _messageBus.SendMessage(new UpdateRelationResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update relation {RecordId}.", request.Record.Id);
                _messageBus.SendMessage(new UpdateRelationResponse(request.RequestId, false, ex.Message));
            }
        }

        // Обрабатывает команду на удаление связи.
        private async Task HandleDeleteRelationAsync(DeleteRelationRequest request)
        {
            try
            {
                await _dataProviderService.DeleteRelationAsync(request.Id);
                _messageBus.SendMessage(new DeleteRelationResponse(request.RequestId, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete relation {RecordId}.", request.Id);
                _messageBus.SendMessage(new DeleteRelationResponse(request.RequestId, false, ex.Message));
            }
        }

        // Снимает все bus-подписки при остановке mini-app.
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
