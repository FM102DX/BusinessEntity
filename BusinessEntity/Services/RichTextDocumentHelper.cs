using BusinessEntity.Core.BaseClasses.Relations;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using BusinessEntity.Core.Services;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;

namespace BusinessEntity.Services
{
    // Инкапсулирует все rich-text операции и использует BusinessEntityHelper как базовый entity-layer.
    public class RichTextDocumentHelper
    {
        // Базовый helper обычных business-entity операций.
        private readonly BusinessEntityHelper _businessEntityHelper;
        // Коннектор data-provider нужен для технических rich-text данных: chunks и embedded files.
        private readonly IDataProviderConnector _dataProviderConnector;
        // Фабрика нужна для сборки runtime entity rich-text документа.
        private readonly IBusinessEntityFactory _businessEntityFactory;

        // Подключает базовый helper и технические зависимости rich-text storage.
        public RichTextDocumentHelper(
            BusinessEntityHelper businessEntityHelper,
            IDataProviderConnector dataProviderConnector,
            IBusinessEntityFactory businessEntityFactory)
        {
            _businessEntityHelper = businessEntityHelper ?? throw new ArgumentNullException(nameof(businessEntityHelper));
            _dataProviderConnector = dataProviderConnector ?? throw new ArgumentNullException(nameof(dataProviderConnector));
            _businessEntityFactory = businessEntityFactory ?? throw new ArgumentNullException(nameof(businessEntityFactory));
        }

        /// <summary>
        /// Создает новый пустой rich-text документ под папкой или пространством.
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> CreateRichTextDocumentAsync(
            BusinessEntity.Core.Classes.BusinessEntity parent,
            CancellationToken ct = default)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (parent.EntityType != BusinessEntityTypeEnum.Folder && parent.EntityType != BusinessEntityTypeEnum.Space)
            {
                throw new ArgumentException("Parent must be a Folder or Space", nameof(parent));
            }

            // Имя rich-text документа генерируем по той же схеме, что и остальные дочерние элементы дерева.
            var name = await GetNewRichTextDocumentNameAsync(parent, ct);

            // Базовую entity создаем как подтип документа, чтобы дерево и страницы видели корректную предметную роль.
            var entity = CreateRichTextDocumentEntity(name);
            await _dataProviderConnector.AddAsync(entity, cancellationToken: ct);

            // Связь дерева rich-text документ не инкапсулирует сам — она идет через обычный helper relations.
            await _businessEntityHelper.CreateRelation(
                parent,
                entity,
                new MacroRelationType { RelationType = BusinessEntityRelationTypeEnum.Contains });

            // На старте у rich-text документа всегда есть manifest и один пустой chunk.
            var manifest = new RichTextDocument
            {
                Name = name,
                Tag = BusinessEntityTypeEnum.RichTextDocument.ToString()
            };

            var initialChunk = new RichTextDocumentChunk
            {
                BusinessEntityId = entity.Id,
                SortOrder = 0,
                Blocks = new List<RichTextBlock>()
            };

            await SaveRichTextDocumentAsync(
                entity,
                manifest,
                new[] { initialChunk },
                Array.Empty<RichTextEmbeddedFile>(),
                replaceExistingFiles: true,
                ct);

            return entity;
        }

        /// <summary>
        /// Загружает readonly-снимок rich-text документа: entity, manifest и набор chunk-ов.
        /// </summary>
        public async Task<RichTextDocumentSnapshot?> GetRichTextDocumentSnapshotAsync(Guid entityId, CancellationToken ct = default)
        {
            var entity = await _businessEntityHelper.GetBusinessEntityById(entityId);
            if (entity == null || entity.EntityType != BusinessEntityTypeEnum.RichTextDocument)
            {
                return null;
            }

            // Manifest читаем через общий typed entity-path helper-а, а технические chunks — напрямую из data-provider.
            var typedEntity = await _businessEntityHelper.GetEntityWithDataAsync<RichTextDocument>(entity.Id, ct);
            var manifest = typedEntity?.Data ?? new RichTextDocument();
            var chunks = await _dataProviderConnector.GetRichTextChunksAsync(entity.Id, ct);

            return new RichTextDocumentSnapshot
            {
                Entity = entity,
                Manifest = manifest,
                Chunks = chunks
            };
        }

        /// <summary>
        /// Возвращает embedded-файл rich-text документа для HTTP-выдачи.
        /// </summary>
        public Task<RichTextEmbeddedFileContent?> GetRichTextEmbeddedFileAsync(
            Guid entityId,
            string imageId,
            string variant,
            CancellationToken ct = default)
        {
            return _dataProviderConnector.GetRichTextEmbeddedFileAsync(entityId, imageId, variant, ct);
        }

        /// <summary>
        /// Сохраняет rich-text документ как fan-out: entity, manifest, chunks и embedded files.
        /// </summary>
        public async Task SaveRichTextDocumentAsync(
            BusinessEntity.Core.Classes.BusinessEntity entity,
            RichTextDocument manifest,
            IReadOnlyList<RichTextDocumentChunk> chunks,
            IReadOnlyList<RichTextEmbeddedFile>? files,
            bool replaceExistingFiles,
            CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            // Нормализуем entity как document-подтип rich-text.
            entity.EntityType = BusinessEntityTypeEnum.RichTextDocument;
            entity.BusinessEntityType = BusinessEntityTypeEnum.Document;
            entity.LastModifiedDate = DateTime.UtcNow;

            // Manifest должен быть синхронизирован с основной entity до сохранения через базовый helper.
            manifest.Id = entity.Id;
            manifest.Name = entity.Name;
            manifest.EntityType = BusinessEntityTypeEnum.RichTextDocument;
            manifest.LastModifiedDate = entity.LastModifiedDate;
            if (manifest.CreatedDate == default)
            {
                manifest.CreatedDate = entity.CreatedDate;
            }

            if (string.IsNullOrWhiteSpace(manifest.Tag))
            {
                manifest.Tag = BusinessEntityTypeEnum.RichTextDocument.ToString();
            }

            // Entity и manifest идут через общий helper-path, чтобы storage-формализация оставалась централизованной.
            await _businessEntityHelper.SaveEntity(entity, manifest);

            // Техническое rich-text содержимое сохраняется отдельным storage fan-out.
            await _dataProviderConnector.ReplaceRichTextChunksAsync(entity.Id, chunks ?? Array.Empty<RichTextDocumentChunk>(), ct);
            await _dataProviderConnector.SaveRichTextEmbeddedFilesAsync(
                entity.Id,
                files ?? Array.Empty<RichTextEmbeddedFile>(),
                replaceExistingFiles,
                ct);
        }

        /// <summary>
        /// Генерирует следующее имя rich-text документа среди дочерних элементов родителя.
        /// </summary>
        private async Task<string> GetNewRichTextDocumentNameAsync(
            BusinessEntity.Core.Classes.BusinessEntity parent,
            CancellationToken ct)
        {
            var baseName = $"New{BusinessEntityTypeEnum.RichTextDocument}";
            var children = await _businessEntityHelper.GetContainedEntitiesAsync(parent.Id, ct);
            var sameTypeChildren = children
                .Where(c => c.EntityType == BusinessEntityTypeEnum.RichTextDocument)
                .Select(c => c.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var i = 1; ; i++)
            {
                var candidateName = $"{baseName}{i}";
                if (!sameTypeChildren.Contains(candidateName))
                {
                    return candidateName;
                }
            }
        }

        /// <summary>
        /// Создает runtime entity rich-text документа как document-подтип с пустым manifest.
        /// </summary>
        private BusinessEntity.Core.Classes.BusinessEntity CreateRichTextDocumentEntity(string name)
        {
            var entity = _businessEntityFactory.Create(
                BusinessEntityTypeEnum.RichTextDocument,
                new RichTextDocument
                {
                    Name = name,
                    Tag = BusinessEntityTypeEnum.RichTextDocument.ToString()
                },
                name);

            entity.BusinessEntityType = BusinessEntityTypeEnum.Document;
            return entity;
        }
    }
}
