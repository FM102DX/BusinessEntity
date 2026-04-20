using System.Text.Json;
using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Внутренний сервис mini-app, который выполняет реальные CRUD-операции поверх DTO-хранилища.
    /// </summary>
    internal sealed class DataProviderService : IDataProviderCrudService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly DataProviderState _state;

        // Получает доступ к репозиториям mini-app через внутреннее состояние.
        public DataProviderService(DataProviderState state)
        {
            _state = state;
        }

        // Читает все DTO сущностей и маппит их в runtime BusinessEntity.
        public async Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var entities = await _state.BusinessEntityRepository.GetAllAsync(ct: cancellationToken);
            return entities.Select(DataProviderMapper.ToBusinessEntity).ToList();
        }

        // Читает одну DTO сущности и маппит её в runtime BusinessEntity.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await _state.BusinessEntityRepository.GetByIdAsync(id, cancellationToken);
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
            var entity = await _state.BusinessEntityRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"BusinessEntity with id '{id}' was not found.");
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

                await _state.BusinessEntityDataRepository.AddAsync(dto, cancellationToken);
                return;
            }

            dto.Data = payload;
            dto.LastModifiedDate = DateTime.UtcNow;
            await _state.BusinessEntityDataRepository.UpdateAsync(dto, cancellationToken);
        }

        // Преобразует runtime сущность в DTO и сохраняет её.
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> AddAsync(BusinessEntity.Core.Classes.BusinessEntity entity, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(entity);
            var saved = await _state.BusinessEntityRepository.AddAsync(dto, cancellationToken);
            return DataProviderMapper.ToBusinessEntity(saved);
        }

        // Преобразует runtime сущность в DTO и обновляет её в хранилище.
        public async Task UpdateAsync(BusinessEntity.Core.Classes.BusinessEntity entity, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(entity);
            await _state.BusinessEntityRepository.UpdateAsync(dto, cancellationToken);
        }

        // Удаляет сущность, её payload и все связанные relation-записи.
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var dataDto = await FindDataDtoAsync(id, cancellationToken);
            if (dataDto != null)
            {
                await _state.BusinessEntityDataRepository.DeleteAsync(dataDto.Id, cancellationToken);
            }

            var relations = await _state.BusinessEntityRelationRepository.GetAllAsync(ct: cancellationToken);
            foreach (var relation in relations.Where(r => r.ObjectAId == id || r.ObjectBId == id))
            {
                await _state.BusinessEntityRelationRepository.DeleteAsync(relation.Id, cancellationToken);
            }

            await _state.BusinessEntityRepository.DeleteAsync(id, cancellationToken);
        }

        // Читает все relation DTO и маппит их в runtime BusinessEntityRelation.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelationsAsync(CancellationToken cancellationToken = default)
        {
            var relations = await _state.BusinessEntityRelationRepository.GetAllAsync(ct: cancellationToken);
            return relations.Select(DataProviderMapper.ToBusinessEntityRelation).ToList();
        }

        // Читает relation DTO между двумя сущностями и маппит их в runtime BusinessEntityRelation.
        public async Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsAsync(Guid objectAId, Guid objectBId, CancellationToken cancellationToken = default)
        {
            var relations = await _state.BusinessEntityRelationRepository.GetAllAsync(
                r => (r.ObjectAId == objectAId && r.ObjectBId == objectBId) || (r.ObjectAId == objectBId && r.ObjectBId == objectAId),
                ct: cancellationToken);
            return relations.Select(DataProviderMapper.ToBusinessEntityRelation).ToList();
        }

        // Читает одну relation DTO и маппит её в runtime BusinessEntityRelation.
        public async Task<BusinessEntityRelation?> GetRelationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var relation = await _state.BusinessEntityRelationRepository.GetByIdAsync(id, cancellationToken);
            return relation == null ? null : DataProviderMapper.ToBusinessEntityRelation(relation);
        }

        // Преобразует runtime BusinessEntityRelation в DTO и сохраняет её.
        public async Task<BusinessEntityRelation> CreateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(relation);
            var saved = await _state.BusinessEntityRelationRepository.AddAsync(dto, cancellationToken);
            return DataProviderMapper.ToBusinessEntityRelation(saved);
        }

        // Преобразует runtime BusinessEntityRelation в DTO и обновляет её.
        public async Task UpdateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default)
        {
            var dto = DataProviderMapper.ToDto(relation);
            await _state.BusinessEntityRelationRepository.UpdateAsync(dto, cancellationToken);
        }

        // Удаляет relation-запись по id.
        public Task DeleteRelationAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _state.BusinessEntityRelationRepository.DeleteAsync(id, cancellationToken);
        }

        // Ищет первую data-запись, привязанную к конкретной сущности.
        private async Task<BusinessEntityDataDto?> FindDataDtoAsync(Guid businessEntityId, CancellationToken cancellationToken)
        {
            var dataItems = await _state.BusinessEntityDataRepository.GetAllAsync(d => d.BusinessEntityId == businessEntityId, 1, cancellationToken);
            return dataItems.FirstOrDefault();
        }
    }
}
