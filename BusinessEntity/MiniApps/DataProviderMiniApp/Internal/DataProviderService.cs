using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal.Conversion;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal
{
    /// <summary>
    /// Внутренний сервис mini-app, который выполняет реальные CRUD-операции поверх DTO-хранилища.
    /// </summary>
    internal sealed class DataProviderService : IDataProviderCrudService
    {
        private readonly IAsyncRepository<BusinessEntityDto> _businessEntityRepository;
        private readonly IAsyncRepository<BusinessEntityDataDto> _businessEntityDataRepository;
        private readonly IAsyncRepository<BusinessEntityDataChunkDto> _businessEntityDataChunkRepository;
        private readonly IAsyncRepository<BusinessEntityRelationDto> _businessEntityRelationRepository;
        private readonly IAsyncRepository<BusinessEntityPropertyDto> _businessEntityPropertyRepository;
        private readonly IAsyncRepository<BusinessEntityDataPropertyDto> _businessEntityDataPropertyRepository;
        private readonly IAsyncRepository<BusinessEntityDataChunkPropertyDto> _businessEntityDataChunkPropertyRepository;
        private readonly EntityDataStorageCodec _entityDataStorageCodec;
        private readonly RichTextDocumentFileStorageService _richTextDocumentFileStorageService;
        private readonly IWebLoggerService? _webLogger;

        // Получает typed-репозитории mini-app напрямую из DI-контейнера.
        public DataProviderService(
            IAsyncRepository<BusinessEntityDto> businessEntityRepository,
            IAsyncRepository<BusinessEntityDataDto> businessEntityDataRepository,
            IAsyncRepository<BusinessEntityDataChunkDto> businessEntityDataChunkRepository,
            IAsyncRepository<BusinessEntityRelationDto> businessEntityRelationRepository,
            IAsyncRepository<BusinessEntityPropertyDto> businessEntityPropertyRepository,
            IAsyncRepository<BusinessEntityDataPropertyDto> businessEntityDataPropertyRepository,
            IAsyncRepository<BusinessEntityDataChunkPropertyDto> businessEntityDataChunkPropertyRepository,
            EntityDataStorageCodec entityDataStorageCodec,
            RichTextDocumentFileStorageService richTextDocumentFileStorageService,
            IWebLoggerService? webLogger)
        {
            _businessEntityRepository = businessEntityRepository;
            _businessEntityDataRepository = businessEntityDataRepository;
            _businessEntityDataChunkRepository = businessEntityDataChunkRepository;
            _businessEntityRelationRepository = businessEntityRelationRepository;
            _businessEntityPropertyRepository = businessEntityPropertyRepository;
            _businessEntityDataPropertyRepository = businessEntityDataPropertyRepository;
            _businessEntityDataChunkPropertyRepository = businessEntityDataChunkPropertyRepository;
            _entityDataStorageCodec = entityDataStorageCodec;
            _richTextDocumentFileStorageService = richTextDocumentFileStorageService;
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

        // Читает JSON-envelope payload и десериализует его в нужный тип.
        public async Task<TData?> GetDataAsync<TData>(Guid id, CancellationToken cancellationToken = default)
            where TData : class, IBusinessEntityData
        {
            var entity = await _businessEntityRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                return default;
            }

            var payload = await GetDataPayloadAsync(id, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return default;
            }

            var envelope = DataPayloadEnvelopeSerializer.ReadEnvelope(payload);
            var data = _entityDataStorageCodec.DeserializePayloadBody<TData>(entity.EntityType, envelope.PayloadJson);
            ApplyEntityMetadata(entity, data);
            return data;
        }

        // Сериализует типизированный payload в raw JSON и сохраняет его как versioned envelope.
        public async Task UpdateDataAsync<TData>(Guid id, TData data, CancellationToken cancellationToken = default)
            where TData : class, IBusinessEntityData
        {
            var payload = _entityDataStorageCodec.SerializePayload(data);
            await UpdateDataPayloadAsync(id, payload, cancellationToken);
        }

        // Возвращает канонический JSON-envelope payload без десериализации.
        public async Task<string?> GetDataPayloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var dto = await FindDataDtoAsync(id, cancellationToken);
            return dto?.Data;
        }

        // Создаёт или обновляет envelope payload для сущности.
        public async Task UpdateDataPayloadAsync(Guid id, string payloadJson, CancellationToken cancellationToken = default)
        {
            var entity = await _businessEntityRepository.GetByIdAsync(id, cancellationToken);
            if (entity == null)
            {
                throw new KeyNotFoundException($"BusinessEntityData with id '{id}' was not found.");
            }

            var envelopeJson = DataPayloadEnvelopeSerializer.CreateEnvelopeJson(entity, payloadJson);
            var dto = await FindDataDtoAsync(id, cancellationToken);

            if (dto == null)
            {
                dto = new BusinessEntityDataDto
                {
                    Id = id,
                    BusinessEntityId = id,
                    Data = envelopeJson
                };

                // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-data-dto] Создан DTO payload entityId={id} dtoId={dto.Id} payloadLength={DataPayloadEnvelopeSerializer.GetJsonLength(envelopeJson)}");
                await _businessEntityDataRepository.AddAsync(dto, cancellationToken);
                // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-data-dto] DTO payload записан в хранилище entityId={id} dtoId={dto.Id} payloadLength={DataPayloadEnvelopeSerializer.GetJsonLength(envelopeJson)}");
                return;
            }

            dto.Data = envelopeJson;
            dto.LastModifiedDate = DateTime.UtcNow;
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:map] [business-entity-data-dto] Обновляем DTO payload entityId={id} dtoId={dto.Id} payloadLength={DataPayloadEnvelopeSerializer.GetJsonLength(envelopeJson)}");
            await _businessEntityDataRepository.UpdateAsync(dto, cancellationToken);
            // _webLogger?.Information($"[мини-апп:data-provider] [dto:write] [business-entity-data-dto] DTO payload обновлен в хранилище entityId={id} dtoId={dto.Id} payloadLength={DataPayloadEnvelopeSerializer.GetJsonLength(envelopeJson)}");
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
            await DeleteRichTextStorageAsync(id, cancellationToken);

            var dataDto = await FindDataDtoAsync(id, cancellationToken);
            if (dataDto != null)
            {
                await DeletePropertiesAsync(_businessEntityDataPropertyRepository, dataDto.Id, cancellationToken);
                await _businessEntityDataRepository.DeleteAsync(dataDto.Id, cancellationToken);
            }

            var relations = await _businessEntityRelationRepository.GetAllAsync(ct: cancellationToken);
            foreach (var relation in relations.Where(r => r.ObjectAId == id || r.ObjectBId == id))
            {
                await _businessEntityRelationRepository.DeleteAsync(relation.Id, cancellationToken);
            }

            await DeletePropertiesAsync(_businessEntityPropertyRepository, id, cancellationToken);
            await _businessEntityRepository.DeleteAsync(id, cancellationToken);
        }

        // Полностью очищает все DTO-таблицы mini-app для debug re-seed сценария.
        public async Task ClearAllAsync(CancellationToken cancellationToken = default)
        {
            await _businessEntityDataChunkPropertyRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityDataPropertyRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityPropertyRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityDataChunkRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityDataRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityRelationRepository.DeleteAllAsync(cancellationToken);
            await _businessEntityRepository.DeleteAllAsync(cancellationToken);
            _richTextDocumentFileStorageService.DeleteAll();
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

        // Возвращает все технические rich-text чанки документа в порядке SortOrder.
        public async Task<IReadOnlyList<RichTextDocumentChunk>> GetRichTextChunksAsync(Guid businessEntityId, CancellationToken cancellationToken = default)
        {
            var chunkDtos = await _businessEntityDataChunkRepository.GetAllAsync(
                d => d.BusinessEntityId == businessEntityId,
                ct: cancellationToken);

            return chunkDtos
                .OrderBy(d => d.SortOrder)
                .Select(MapChunkDtoToRuntime)
                .ToList();
        }

        // Полностью заменяет chunk-body документа новым набором чанков.
        public async Task ReplaceRichTextChunksAsync(Guid businessEntityId, IReadOnlyList<RichTextDocumentChunk> chunks, CancellationToken cancellationToken = default)
        {
            var existingChunkDtos = await _businessEntityDataChunkRepository.GetAllAsync(
                d => d.BusinessEntityId == businessEntityId,
                ct: cancellationToken);

            foreach (var existingChunk in existingChunkDtos)
            {
                await DeletePropertiesAsync(_businessEntityDataChunkPropertyRepository, existingChunk.Id, cancellationToken);
                await _businessEntityDataChunkRepository.DeleteAsync(existingChunk.Id, cancellationToken);
            }

            if (chunks == null || chunks.Count == 0)
            {
                return;
            }

            var sortOrder = 0L;
            foreach (var chunk in chunks.OrderBy(c => c.SortOrder))
            {
                var dto = MapChunkRuntimeToDto(businessEntityId, chunk, sortOrder++);
                await _businessEntityDataChunkRepository.AddAsync(dto, cancellationToken);
            }
        }

        // Сохраняет embedded-файлы rich-text документа в локальное техническое storage.
        public Task SaveRichTextEmbeddedFilesAsync(
            Guid businessEntityId,
            IReadOnlyList<RichTextEmbeddedFile> files,
            bool replaceExistingFiles,
            CancellationToken cancellationToken = default)
        {
            return _richTextDocumentFileStorageService.SaveFilesAsync(
                businessEntityId,
                files,
                replaceExistingFiles,
                cancellationToken);
        }

        // Читает embedded-файл rich-text документа.
        public Task<RichTextEmbeddedFileContent?> GetRichTextEmbeddedFileAsync(
            Guid businessEntityId,
            string imageId,
            string variant,
            CancellationToken cancellationToken = default)
        {
            return _richTextDocumentFileStorageService.GetFileAsync(
                businessEntityId,
                imageId,
                variant,
                cancellationToken);
        }

        // Полностью удаляет техническое rich-text storage документа.
        public async Task DeleteRichTextStorageAsync(Guid businessEntityId, CancellationToken cancellationToken = default)
        {
            var chunkDtos = await _businessEntityDataChunkRepository.GetAllAsync(
                d => d.BusinessEntityId == businessEntityId,
                ct: cancellationToken);

            foreach (var chunkDto in chunkDtos)
            {
                await DeletePropertiesAsync(_businessEntityDataChunkPropertyRepository, chunkDto.Id, cancellationToken);
                await _businessEntityDataChunkRepository.DeleteAsync(chunkDto.Id, cancellationToken);
            }

            _richTextDocumentFileStorageService.DeleteDocumentFolder(businessEntityId);
        }

        // Ищет первую data-запись, привязанную к конкретной сущности.
        private async Task<BusinessEntityDataDto?> FindDataDtoAsync(Guid businessEntityId, CancellationToken cancellationToken)
        {
            var dataItems = await _businessEntityDataRepository.GetAllAsync(d => d.BusinessEntityId == businessEntityId, 1, cancellationToken);
            return dataItems.FirstOrDefault();
        }

        // Удаляет property-строки, привязанные к конкретной родительской DTO-записи.
        private static async Task DeletePropertiesAsync<TProperty>(
            IAsyncRepository<TProperty> repository,
            Guid parentEntityId,
            CancellationToken cancellationToken)
            where TProperty : class, IPropertyDto
        {
            var properties = await repository.GetAllAsync(p => p.ParentEntityId == parentEntityId, ct: cancellationToken);
            foreach (var property in properties)
            {
                await repository.DeleteAsync(property.Id, cancellationToken);
            }
        }

        // Преобразует DTO-строку чанка в runtime-объект rich-text чанка.
        private static RichTextDocumentChunk MapChunkDtoToRuntime(BusinessEntityDataChunkDto dto)
        {
            var blocks = RichTextChunkStorageSerializer.DeserializeChunkData(dto.Data);
            return new RichTextDocumentChunk
            {
                Id = dto.Id,
                BusinessEntityId = dto.BusinessEntityId,
                SortOrder = dto.SortOrder,
                Blocks = blocks,
                PlainText = dto.PlainText ?? string.Empty,
                HtmlCache = dto.HtmlCache ?? string.Empty,
                BlockCount = dto.BlockCount,
                CharCount = dto.CharCount,
                DataSizeBytes = dto.DataSizeBytes,
                Version = dto.Version,
                Checksum = dto.Checksum ?? string.Empty
            };
        }

        // Преобразует runtime rich-text chunk в технический DTO storage-слоя.
        private static BusinessEntityDataChunkDto MapChunkRuntimeToDto(Guid businessEntityId, RichTextDocumentChunk chunk, long sortOrder)
        {
            var dataJson = RichTextChunkStorageSerializer.SerializeChunkData(chunk.Blocks);
            return new BusinessEntityDataChunkDto
            {
                Id = chunk.Id == Guid.Empty ? Guid.NewGuid() : chunk.Id,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
                BusinessEntityId = businessEntityId,
                SortOrder = sortOrder,
                Data = dataJson,
                PlainText = RichTextChunkStorageSerializer.BuildPlainText(chunk.Blocks),
                HtmlCache = RichTextChunkStorageSerializer.BuildHtmlCache(businessEntityId, chunk.Blocks),
                BlockCount = chunk.Blocks?.Count ?? 0,
                CharCount = RichTextChunkStorageSerializer.BuildCharCount(chunk.Blocks),
                DataSizeBytes = DataPayloadEnvelopeSerializer.GetJsonLength(dataJson),
                Version = chunk.Version <= 0 ? 1 : chunk.Version,
                Checksum = RichTextChunkStorageSerializer.BuildChecksum(dataJson)
            };
        }

        // Накладывает общие entity-метаданные поверх typed payload после чтения из storage.
        private static void ApplyEntityMetadata(BusinessEntityDto entity, IBusinessEntityData data)
        {
            data.Id = entity.Id;
            data.CreatedDate = entity.CreatedDate;
            data.LastModifiedDate = entity.LastModifiedDate;
            data.Name = entity.Name;
            data.EntityType = entity.EntityType;

            if (string.IsNullOrWhiteSpace(data.Tag))
            {
                data.Tag = DataPayloadEnvelopeSerializer.GetStorageKind(entity.EntityType);
            }
        }
    }
}
