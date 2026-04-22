using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages;
using ReactiveUI;
using System.Text.Json;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Connectors
{
    /// <summary>
    /// Connector mini-app хранения данных.
    /// Инкапсулирует только bus-roundtrip для типизированных storage-запросов.
    /// </summary>
    public sealed class DataProviderConnector : IDataProviderConnector
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IMessageBus _messageBus;

        /// <summary>
        /// Инициализирует connector и гарантирует материализацию mini-app перед первым запросом.
        /// </summary>
        // Поднимает инициализацию mini-app и сохраняет bus для request/response обмена.
        public DataProviderConnector(IMessageBus messageBus, IDataProviderMiniApp dataProviderMiniApp)
        {
            _messageBus = messageBus;
            dataProviderMiniApp.EnsureInitialized();
        }

        // Запрашивает полный список бизнес-сущностей через bus.
        public async Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetBusinessEntitiesRequest, GetBusinessEntitiesResponse>(
                new GetBusinessEntitiesRequest(requestId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Records;
        }

        // Запрашивает одну бизнес-сущность по id через bus.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetBusinessEntityByIdRequest, GetBusinessEntityByIdResponse>(
                new GetBusinessEntityByIdRequest(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record;
        }

        // Получает payload сущности и десериализует его в нужный тип.
        public async Task<T?> GetDataAsync<T>(Guid id, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetBusinessEntityDataRequest, GetBusinessEntityDataResponse>(
                new GetBusinessEntityDataRequest(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            if (response.Data == null || response.Data.Length == 0)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(response.Data, JsonOptions);
        }

        // Сериализует payload и отправляет команду на его сохранение.
        public async Task UpdateDataAsync<T>(Guid id, T data, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var payload = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
            var response = await SendAndReceiveAsync<UpdateBusinessEntityDataRequest, UpdateBusinessEntityDataResponse>(
                new UpdateBusinessEntityDataRequest(requestId, id, payload),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to update data for business entityData '{id}'.");
            }
        }

        // Отправляет команду на создание бизнес-сущности.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> AddAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<AddBusinessEntityRequest, AddBusinessEntityResponse>(
                new AddBusinessEntityRequest(requestId, entityData),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record ?? throw new InvalidOperationException("DataProvider returned null business entityData after AddAsync.");
        }

        // Отправляет команду на обновление бизнес-сущности.
        public async Task UpdateAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<UpdateBusinessEntityRequest, UpdateBusinessEntityResponse>(
                new UpdateBusinessEntityRequest(requestId, entityData),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);

            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to update business entityData '{entityData.Id}'.");
            }
        }

        // Отправляет команду на удаление бизнес-сущности.
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<DeleteBusinessEntityRequest, DeleteBusinessEntityResponse>(
                new DeleteBusinessEntityRequest(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);

            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to delete business entityData '{id}'.");
            }
        }

        // Отправляет debug-команду на полную очистку DTO-хранилища mini-app.
        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<ClearDataProviderStorageRequest, ClearDataProviderStorageResponse>(
                new ClearDataProviderStorageRequest(requestId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            if (!response.Success)
            {
                throw new InvalidOperationException("DataProvider failed to clear storage.");
            }
        }

        // Запрашивает полный список связей между сущностями.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelationsAsync(CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetAllRelationsRequest, GetAllRelationsResponse>(
                new GetAllRelationsRequest(requestId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Records;
        }

        // Запрашивает связи между двумя бизнес-объектами.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsAsync(Guid objectAId, Guid objectBId, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetRelationsRequest, GetRelationsResponse>(
                new GetRelationsRequest(requestId, objectAId, objectBId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Records;
        }

        // Запрашивает одну связь по её идентификатору.
        public async Task<BusinessEntityRelation?> GetRelationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetRelationByIdRequest, GetRelationByIdResponse>(
                new GetRelationByIdRequest(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record;
        }

        // Отправляет команду на создание связи.
        public async Task<BusinessEntityRelation> CreateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<CreateRelationRequest, CreateRelationResponse>(
                new CreateRelationRequest(requestId, relation),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record ?? throw new InvalidOperationException("DataProvider returned null relation after CreateRelationAsync.");
        }

        // Отправляет команду на обновление связи.
        public async Task UpdateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<UpdateRelationRequest, UpdateRelationResponse>(
                new UpdateRelationRequest(requestId, relation),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to update relation '{relation.Id}'.");
            }
        }

        // Отправляет команду на удаление связи.
        public async Task DeleteRelationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<DeleteRelationRequest, DeleteRelationResponse>(
                new DeleteRelationRequest(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to delete relation '{id}'.");
            }
        }

        // Выполняет общий request/response цикл поверх IMessageBus.
        private async Task<TResponse> SendAndReceiveAsync<TRequest, TResponse>(
            TRequest request,
            Func<TResponse, Guid> requestIdSelector,
            Guid expectedRequestId,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            IDisposable? subscription = null;

            subscription = _messageBus
                .Listen<TResponse>()
                .Subscribe(response =>
                {
                    if (requestIdSelector(response) != expectedRequestId)
                    {
                        return;
                    }

                    subscription?.Dispose();
                    completion.TrySetResult(response);
                });

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                subscription?.Dispose();
                completion.TrySetCanceled(cancellationToken);
            });

            _messageBus.SendMessage(request);
            return await completion.Task;
        }

        // Превращает ошибку из ответа в исключение connector-уровня.
        private static void EnsureNoError(string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
