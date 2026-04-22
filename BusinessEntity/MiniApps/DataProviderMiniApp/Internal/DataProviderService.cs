using System.Text.Json;
using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Внутренний сервис mini-app, который выполняет реальные CRUD-операции поверх DTO-хранилища.
    /// </summary>
    internal sealed class DataProviderService : IDataProviderCrudService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IAsyncRepository<BusinessEntityDto> _businessEntityRepository;
        private readonly IAsyncRepository<BusinessEntityDataDto> _businessEntityDataRepository;
        private readonly IAsyncRepository<BusinessEntityRelationDto> _businessEntityRelationRepository;
        private readonly IWebLoggerService? _webLogger;

        // Получает typed-репозитории mini-app напрямую из DI-контейнера.
        public DataProviderService(
            IAsyncRepository<BusinessEntityDto> businessEntityRepository,
            IAsyncRepository<BusinessEntityDataDto> businessEntityDataRepository,
            IAsyncRepository<BusinessEntityRelationDto> businessEntityRelationRepository,
            IWebLoggerService? webLogger)
        {
            _businessEntityRepository = businessEntityRepository;
            _businessEntityDataRepository = businessEntityDataRepository;
            _businessEntityRelationRepository = businessEntityRelationRepository;
            _webLogger = webLogger;
        }

        // Читает все DTO сущностей и маппит их в runtime BusinessEntityData.
        public async Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _businessEntityRepository.GetAllAsync(ct: cancellationToken);
            return entities.Select(DataProviderMapper.ToBusinessEntity).ToList();
        }

        // Читает одну DTO сущности и маппит её в runtime BusinessEntityData.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _businessEntityRepository.GetByIdAsync(id, cancellationToken);
            return entity == null ? null : DataProviderMapper.ToBusinessEntity(entity);
        }

        // Читает бинарный payload и десериализует его в нужный тип.
        public async Task<T?> GetDataAsync<T>(Guid id, CancellationToken cancellationToken = default)
        {
            var payload = await GetDataPayloadAsync(id, cancellationToken);
            if (payload == null || payload.Length == 0)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(payload, JsonOptions);
        }

        // Сериализует типизированный payload и сохраняет его как byte[].
        public async Task UpdateDataAsync<T>(Guid id, T data, CancellationToken cancellationToken = default)
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
            await UpdateDataPayloadAsync(id, payload, cancellationToken);
        }

        // Возвращает сырые байты payload без десериализации.
        public async Task<byte[]?> GetDataPayloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var dto = await FindDataDtoAsync(id, cancellationToken);
            return dto?.Data;
        }

        // Создаёт или обновляет raw payload для сущности.
        public async Task UpdateDataPayloadAsync(Guid id, byte[] payload, CancellationToken cancellationToken = default)
        {
            var entity = await _businessEntityRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"BusinessEntityData with id '{id}' was not found.");
            }

            var dto = await FindDataDtoAsync(id, cancellationToken);

            if (dto == null)
            {
                dto = new BusinessEntityDataDto
                {
                    Id = id,
                    BusinessEntityId = id,
                    Data = payload
                };

                // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-data-dto] Создан DTO payload entityId={id} dtoId={dto.Id} payloadLength={payload.Length}");
                await _businessEntityDataRepository.AddAsync(dto, cancellationToken);
                // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-data-dto] DTO payload записан в хранилище entityId={id} dtoId={dto.Id} payloadLength={payload.Length}");
                return;
            }

            dto.Data = payload;
            dto.LastModifiedDate = DateTime.UtcNow;
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-data-dto] Обновляем DTO payload entityId={id} dtoId={dto.Id} payloadLength={payload.Length}");
            await _businessEntityDataRepository.UpdateAsync(dto, cancellationToken);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-data-dto] DTO payload обновлен в хранилище entityId={id} dtoId={dto.Id} payloadLength={payload.Length}");
        }

        // Преобразует runtime сущность в DTO и сохраняет её.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> AddAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(entityData);
            _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-dto] BusinessEntity -> DTO entityId={entityData.Id} dtoId={dto.Id} type={dto.EntityType} name='{dto.Name}'");
            var saved = await _businessEntityRepository.AddAsync(dto, cancellationToken);
            _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-dto] DTO сущности записан в хранилище entityId={saved.Id} type={saved.EntityType} name='{saved.Name}'");
            return DataProviderMapper.ToBusinessEntity(saved);
        }

        // Преобразует runtime сущность в DTO и обновляет её в хранилище.
        public async Task UpdateAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(entityData);
            _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-dto] Обновляем DTO сущности entityId={entityData.Id} dtoId={dto.Id} type={dto.EntityType} name='{dto.Name}'");
            await _businessEntityRepository.UpdateAsync(dto, cancellationToken);
            _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-dto] DTO сущности обновлен в хранилище entityId={dto.Id} type={dto.EntityType} name='{dto.Name}'");
        }

        // Удаляет сущность, её payload и все связанные relation-записи.
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var dataDto = await FindDataDtoAsync(id, cancellationToken);
            if (dataDto != null)
            {
                await _businessEntityDataRepository.DeleteAsync(dataDto.Id, cancellationToken);
            }

            var relations = await _businessEntityRelationRepository.GetAllAsync(ct: cancellationToken);
            foreach (var relation in relations.Where(r => r.ObjectAId == id || r.ObjectBId == id))
            {
                await _businessEntityRelationRepository.DeleteAsync(relation.Id, cancellationToken);
            }

            await _businessEntityRepository.DeleteAsync(id, cancellationToken);
        }

        // Полностью очищает все DTO-таблицы mini-app для debug re-seed сценария.
        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            await _businessEntityDataRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityRelationRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityRepository.DeleteAllAsync(cancellationToken);
        }

        // Читает все relation DTO и маппит их в runtime BusinessEntityRelation.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelationsAsync(CancellationToken cancellationToken = default)
        {
            var relations = await _businessEntityRelationRepository.GetAllAsync(ct: cancellationToken);
            return relations.Select(DataProviderMapper.ToBusinessEntityRelation).ToList();
        }

        // Читает relation DTO между двумя сущностями и маппит их в runtime BusinessEntityRelation.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsAsync(Guid objectAId, Guid objectBId, CancellationToken cancellationToken = default)
        {
            var relations = await _businessEntityRelationRepository.GetAllAsync(
                r => (r.ObjectAId == objectAId && r.ObjectBId == objectBId) || (r.ObjectAId == objectBId && r.ObjectBId == objectAId),
                ct: cancellationToken);
            return relations.Select(DataProviderMapper.ToBusinessEntityRelation).ToList();
        }

        // Читает одну relation DTO и маппит её в runtime BusinessEntityRelation.
        public async Task<BusinessEntityRelation?> GetRelationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var relation = await _businessEntityRelationRepository.GetByIdAsync(id, cancellationToken);
            return relation == null ? null : DataProviderMapper.ToBusinessEntityRelation(relation);
        }

        // Преобразует runtime BusinessEntityRelation в DTO и сохраняет её.
        public async Task<BusinessEntityRelation> CreateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(relation);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-relation-dto] Relation -> DTO relationId={relation.Id} dtoId={dto.Id} objectA={dto.ObjectAId} objectB={dto.ObjectBId} type={dto.RelationType}");
            var saved = await _businessEntityRelationRepository.AddAsync(dto, cancellationToken);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-relation-dto] DTO relation записан в хранилище relationId={saved.Id} objectA={saved.ObjectAId} objectB={saved.ObjectBId} type={saved.RelationType}");
            return DataProviderMapper.ToBusinessEntityRelation(saved);
        }

        // Преобразует runtime BusinessEntityRelation в DTO и обновляет её.
        public async Task UpdateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(relation);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-relation-dto] Обновляем DTO relation relationId={relation.Id} dtoId={dto.Id} objectA={dto.ObjectAId} objectB={dto.ObjectBId} type={dto.RelationType}");
            await _businessEntityRelationRepository.UpdateAsync(dto, cancellationToken);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-relation-dto] DTO relation обновлен в хранилище relationId={dto.Id} objectA={dto.ObjectAId} objectB={dto.ObjectBId} type={dto.RelationType}");
        }

        // Удаляет relation-запись по id.
        public Task DeleteRelationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _businessEntityRelationRepository.DeleteAsync(id, cancellationToken);
        }

        // Ищет первую data-запись, привязанную к конкретной сущности.
        private async Task<BusinessEntityDataDto?> FindDataDtoAsync(Guid businessEntityId, CancellationToken cancellationToken)
        {
            var dataItems = await _businessEntityDataRepository.GetAllAsync(d => d.BusinessEntityId == businessEntityId, 1, cancellationToken);
            return dataItems.FirstOrDefault();
        }
    }
}
