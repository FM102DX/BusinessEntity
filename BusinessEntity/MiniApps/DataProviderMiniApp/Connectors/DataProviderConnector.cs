using System.Linq.Expressions;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages;
using ReactiveUI;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Connectors
{
    /// <summary>
    /// Connector mini-app хранения данных.
    /// Инкапсулирует только bus-roundtrip для типизированных storage-запросов.
    /// </summary>
    public sealed class DataProviderConnector : IDataProviderConnector
    {
        private readonly IMessageBus _messageBus;

        /// <summary>
        /// Инициализирует connector и гарантирует материализацию mini-app перед первым запросом.
        /// </summary>
        public DataProviderConnector(IMessageBus messageBus, IDataProviderMiniApp dataProviderMiniApp)
        {
            _messageBus = messageBus;
            _ = dataProviderMiniApp;
        }

        /// <summary>
        /// Отправляет запрос на получение списка записей и ждёт типизированный ответ.
        /// </summary>
        public async Task<IReadOnlyList<T>> GetAllAsync<T>(Expression<Func<T, bool>>? filter = null, int? take = null, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetRecordsRequest<T>, GetRecordsResponse<T>>(
                new GetRecordsRequest<T>(requestId, filter, take),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Records;
        }

        /// <summary>
        /// Отправляет запрос на получение записи по идентификатору и ждёт ответ.
        /// </summary>
        public async Task<T?> GetByIdAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetRecordByIdRequest<T>, GetRecordByIdResponse<T>>(
                new GetRecordByIdRequest<T>(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record;
        }

        /// <summary>
        /// Отправляет запрос на проверку существования записи и ждёт ответ.
        /// </summary>
        public async Task<bool> ExistsAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<RecordExistsRequest<T>, RecordExistsResponse<T>>(
                new RecordExistsRequest<T>(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Exists;
        }

        /// <summary>
        /// Отправляет команду на добавление записи и ждёт типизированный ответ.
        /// </summary>
        public async Task<T> AddAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<AddRecordRequest<T>, AddRecordResponse<T>>(
                new AddRecordRequest<T>(requestId, entity),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Record ?? throw new InvalidOperationException($"DataProvider returned null record after AddAsync for '{typeof(T).Name}'.");
        }

        /// <summary>
        /// Отправляет команду на обновление записи и ждёт подтверждение.
        /// </summary>
        public async Task UpdateAsync<T>(T entity, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<UpdateRecordRequest<T>, UpdateRecordResponse<T>>(
                new UpdateRecordRequest<T>(requestId, entity),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);

            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to update '{typeof(T).Name}' with id '{entity.Id}'.");
            }
        }

        /// <summary>
        /// Отправляет команду на удаление записи и ждёт подтверждение.
        /// </summary>
        public async Task DeleteAsync<T>(Guid id, CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<DeleteRecordRequest<T>, DeleteRecordResponse<T>>(
                new DeleteRecordRequest<T>(requestId, id),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);

            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to delete '{typeof(T).Name}' with id '{id}'.");
            }
        }

        /// <summary>
        /// Отправляет запрос количества записей и ждёт ответ.
        /// </summary>
        public async Task<int> GetCountAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<GetRecordCountRequest<T>, GetRecordCountResponse<T>>(
                new GetRecordCountRequest<T>(requestId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);
            return response.Count;
        }

        /// <summary>
        /// Отправляет команду полной очистки хранилища и ждёт подтверждение.
        /// </summary>
        public async Task DeleteAllAsync<T>(CancellationToken cancellationToken = default)
            where T : class, IBaseEntity
        {
            var requestId = Guid.NewGuid();
            var response = await SendAndReceiveAsync<DeleteAllRecordsRequest<T>, DeleteAllRecordsResponse<T>>(
                new DeleteAllRecordsRequest<T>(requestId),
                static result => result.RequestId,
                requestId,
                cancellationToken);

            EnsureNoError(response.ErrorMessage);

            if (!response.Success)
            {
                throw new InvalidOperationException($"DataProvider failed to delete all '{typeof(T).Name}' records.");
            }
        }

        /// <summary>
        /// Выполняет общий bus-roundtrip для request/response сценария.
        /// </summary>
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

        /// <summary>
        /// Превращает storage-error в исключение connector-уровня.
        /// </summary>
        private static void EnsureNoError(string? errorMessage)
        {
            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new InvalidOperationException(errorMessage);
            }
        }
    }
}
