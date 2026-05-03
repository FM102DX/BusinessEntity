using BusinessEntity.Core.BaseClasses.Relations;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using BusinessEntity.Core.Services;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;
using BusinessEntity.Services.RichTextImport;
using BusinessEntity.WebLogger.Services;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BusinessEntity.Services
{
    // Инкапсулирует все rich-text операции и использует BusinessEntityHelper как базовый entity-layer.
    public class RichTextDocumentHelper
    {
        public const int MaxRichTextDocumentTitleLength = 120;

        private const string InvalidTitleCharacters = "<>:\"/\\|?*";

        // Базовый helper обычных business-entity операций.
        private readonly BusinessEntityHelper _businessEntityHelper;
        // Коннектор data-provider нужен для технических rich-text данных: chunks и embedded files.
        private readonly IDataProviderConnector _dataProviderConnector;
        private readonly IAsyncRepository<BusinessEntityDataChunkDto> _businessEntityDataChunkRepository;
        private readonly IAsyncRepository<BusinessEntityDataChunkPropertyDto> _businessEntityDataChunkPropertyRepository;
        private readonly HtmlToRichTextBlocksConverter _htmlToRichTextBlocksConverter;
        private readonly IWebLoggerService? _webLogger;
        // Фабрика нужна для сборки runtime entity rich-text документа.
        private readonly IBusinessEntityFactory _businessEntityFactory;

        // Подключает базовый helper и технические зависимости rich-text storage.
        public RichTextDocumentHelper(
            BusinessEntityHelper businessEntityHelper,
            IDataProviderConnector dataProviderConnector,
            IAsyncRepository<BusinessEntityDataChunkDto> businessEntityDataChunkRepository,
            IAsyncRepository<BusinessEntityDataChunkPropertyDto> businessEntityDataChunkPropertyRepository,
            HtmlToRichTextBlocksConverter htmlToRichTextBlocksConverter,
            IWebLoggerService? webLogger,
            IBusinessEntityFactory businessEntityFactory)
        {
            _businessEntityHelper = businessEntityHelper ?? throw new ArgumentNullException(nameof(businessEntityHelper));
            _dataProviderConnector = dataProviderConnector ?? throw new ArgumentNullException(nameof(dataProviderConnector));
            _businessEntityDataChunkRepository = businessEntityDataChunkRepository ?? throw new ArgumentNullException(nameof(businessEntityDataChunkRepository));
            _businessEntityDataChunkPropertyRepository = businessEntityDataChunkPropertyRepository ?? throw new ArgumentNullException(nameof(businessEntityDataChunkPropertyRepository));
            _htmlToRichTextBlocksConverter = htmlToRichTextBlocksConverter ?? throw new ArgumentNullException(nameof(htmlToRichTextBlocksConverter));
            _webLogger = webLogger;
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
            var shell = await GetRichTextDocumentShellAsync(entityId, ct);
            if (shell == null)
            {
                return null;
            }

            // Legacy full snapshot path. The virtualized viewer uses chunk windows instead.
            var chunks = await _dataProviderConnector.GetRichTextChunksAsync(shell.Entity.Id, ct);

            return new RichTextDocumentSnapshot
            {
                Entity = shell.Entity,
                Manifest = shell.Manifest,
                Chunks = chunks
            };
        }

        /// <summary>
        /// Loads only entity and manifest metadata without body chunks.
        /// </summary>
        public async Task<RichTextDocumentShell?> GetRichTextDocumentShellAsync(Guid entityId, CancellationToken ct = default)
        {
            var entity = await _businessEntityHelper.GetBusinessEntityById(entityId);
            if (entity == null || entity.EntityType != BusinessEntityTypeEnum.RichTextDocument)
            {
                return null;
            }

            var typedEntity = await _businessEntityHelper.GetEntityWithDataAsync<RichTextDocument>(entity.Id, ct);
            return new RichTextDocumentShell
            {
                Entity = entity,
                Manifest = typedEntity?.Data ?? new RichTextDocument()
            };
        }

        public static string FilterRichTextDocumentTitle(string? title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(title.Length);
            var previousWasWhitespace = false;

            foreach (var ch in title)
            {
                if (char.IsControl(ch) || InvalidTitleCharacters.Contains(ch))
                {
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (previousWasWhitespace || builder.Length == 0)
                    {
                        continue;
                    }

                    builder.Append(' ');
                    previousWasWhitespace = true;
                    continue;
                }

                builder.Append(ch);
                previousWasWhitespace = false;

                if (builder.Length >= MaxRichTextDocumentTitleLength)
                {
                    break;
                }
            }

            return builder.ToString().TrimEnd(' ', '.');
        }

        public static string NormalizeRichTextDocumentTitle(string? title)
        {
            var normalized = FilterRichTextDocumentTitle(title);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("Название не может быть пустым.");
            }

            return normalized;
        }

        public async Task<RichTextDocumentTitleSaveResult> SaveRichTextDocumentTitleAsync(
            Guid entityId,
            string? title,
            CancellationToken ct = default)
        {
            if (entityId == Guid.Empty)
            {
                throw new ArgumentException("Entity id is required.", nameof(entityId));
            }

            var normalizedTitle = NormalizeRichTextDocumentTitle(title);
            var shell = await GetRichTextDocumentShellAsync(entityId, ct);
            if (shell == null)
            {
                throw new InvalidOperationException("Rich-text документ не найден.");
            }

            var titleChanged = !string.Equals(shell.Entity.Name, normalizedTitle, StringComparison.Ordinal);
            if (!titleChanged)
            {
                return new RichTextDocumentTitleSaveResult
                {
                    Entity = shell.Entity,
                    Title = normalizedTitle,
                    TitleChanged = false
                };
            }

            shell.Entity.Name = normalizedTitle;
            shell.Entity.EntityType = BusinessEntityTypeEnum.RichTextDocument;
            shell.Entity.BusinessEntityType = BusinessEntityTypeEnum.Document;
            shell.Entity.LastModifiedDate = DateTime.UtcNow;

            shell.Manifest.Id = shell.Entity.Id;
            shell.Manifest.Name = normalizedTitle;
            shell.Manifest.EntityType = BusinessEntityTypeEnum.RichTextDocument;
            shell.Manifest.LastModifiedDate = shell.Entity.LastModifiedDate;
            if (shell.Manifest.CreatedDate == default)
            {
                shell.Manifest.CreatedDate = shell.Entity.CreatedDate;
            }

            if (string.IsNullOrWhiteSpace(shell.Manifest.Tag))
            {
                shell.Manifest.Tag = BusinessEntityTypeEnum.RichTextDocument.ToString();
            }

            await _businessEntityHelper.SaveEntity(shell.Entity, shell.Manifest);

            return new RichTextDocumentTitleSaveResult
            {
                Entity = shell.Entity,
                Title = normalizedTitle,
                TitleChanged = true
            };
        }

        /// <summary>
        /// Loads the rich-text document table of contents from persisted chunk properties and returns it as a tree.
        /// </summary>
        public async Task<IReadOnlyList<RichTextDocumentTableOfContentsEntry>> GetTableOfContentsAsync(Guid entityId, CancellationToken ct = default)
        {
            var entries = await _dataProviderConnector.GetRichTextTableOfContentsEntriesAsync(entityId, ct);
            return BuildTableOfContentsTree(entries);
        }

        /// <summary>
        /// Loads persisted table-of-contents entries progressively by chunk batches.
        /// </summary>
        public async IAsyncEnumerable<IReadOnlyList<RichTextDocumentTableOfContentsEntry>> GetTableOfContentsBatchesAsync(
            Guid entityId,
            int chunkBatchSize = 5,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var normalizedBatchSize = Math.Max(chunkBatchSize, 1);
            var totalChunkCount = await GetChunkCountAsync(entityId, ct);
            if (totalChunkCount <= 0)
            {
                yield return Array.Empty<RichTextDocumentTableOfContentsEntry>();
                yield break;
            }

            var flatEntries = new List<RichTextDocumentTableOfContentsEntry>();
            for (var skip = 0; skip < totalChunkCount; skip += normalizedBatchSize)
            {
                ct.ThrowIfCancellationRequested();

                var chunkDtos = await _businessEntityDataChunkRepository.GetPageAsync(
                    d => d.BusinessEntityId == entityId,
                    d => d.SortOrder,
                    skip: skip,
                    take: normalizedBatchSize,
                    ct: ct);

                foreach (var chunkDto in chunkDtos.OrderBy(d => d.SortOrder))
                {
                    ct.ThrowIfCancellationRequested();

                    var properties = await _businessEntityDataChunkPropertyRepository.GetAllAsync(
                        p => p.ParentEntityId == chunkDto.Id &&
                             p.PropertyType == (int)BusinessEntityDataChunkPropertyTypeEnum.RichDocTableOfContents,
                        ct: ct);

                    foreach (var property in properties)
                    {
                        flatEntries.AddRange(ReadTableOfContentsEntries(property));
                    }
                }

                yield return BuildTableOfContentsTree(flatEntries);
            }
        }

        /// <summary>
        /// Rebuilds persisted chunk table-of-contents properties and returns the fresh tree.
        /// </summary>
        public async Task<IReadOnlyList<RichTextDocumentTableOfContentsEntry>> RebuildTableOfContentsAsync(Guid entityId, CancellationToken ct = default)
        {
            var entries = await _dataProviderConnector.RebuildRichTextTableOfContentsEntriesAsync(entityId, ct);
            return BuildTableOfContentsTree(entries);
        }

        /// <summary>
        /// Returns the number of persisted chunks for virtualized reading.
        /// </summary>
        public Task<int> GetChunkCountAsync(Guid entityId, CancellationToken ct = default)
        {
            return _businessEntityDataChunkRepository.GetCountAsync(
                d => d.BusinessEntityId == entityId,
                ct);
        }

        /// <summary>
        /// Loads a sort-order window of chunks for virtualized reading.
        /// </summary>
        public async Task<RichTextDocumentChunkWindow> GetChunkWindowAsync(
            Guid entityId,
            long startSortOrder,
            int take,
            CancellationToken ct = default)
        {
            var totalCount = await GetChunkCountAsync(entityId, ct);
            if (totalCount <= 0 || take <= 0)
            {
                return new RichTextDocumentChunkWindow
                {
                    BusinessEntityId = entityId,
                    StartSortOrder = 0,
                    TotalChunkCount = totalCount
                };
            }

            var normalizedStart = Math.Clamp(startSortOrder, 0, Math.Max(totalCount - 1, 0));
            var dtos = await _businessEntityDataChunkRepository.GetPageAsync(
                d => d.BusinessEntityId == entityId && d.SortOrder >= normalizedStart,
                d => d.SortOrder,
                skip: 0,
                take: take,
                ct: ct);

            await LogChunkWindowReadAsync(entityId, normalizedStart, take, totalCount, dtos);

            return new RichTextDocumentChunkWindow
            {
                BusinessEntityId = entityId,
                StartSortOrder = normalizedStart,
                TotalChunkCount = totalCount,
                Chunks = dtos.Select(MapChunkDtoToRuntime).ToList()
            };
        }

        /// <summary>
        /// Loads a chunk window centered around a target sort order.
        /// </summary>
        public Task<RichTextDocumentChunkWindow> GetChunkWindowAroundAsync(
            Guid entityId,
            long centerSortOrder,
            int before,
            int after,
            CancellationToken ct = default)
        {
            var normalizedBefore = Math.Max(before, 0);
            var normalizedAfter = Math.Max(after, 0);
            var startSortOrder = Math.Max(centerSortOrder - normalizedBefore, 0);
            return GetChunkWindowAsync(entityId, startSortOrder, normalizedBefore + 1 + normalizedAfter, ct);
        }

        /// <summary>
        /// Saves dirty chunks from rich-document edit mode without replacing the whole chunk set.
        /// </summary>
        public async Task<int> SaveEditedChunksAsync(
            Guid entityId,
            IReadOnlyList<RichTextDocumentChunkEditDraft> drafts,
            CancellationToken ct = default)
        {
            if (entityId == Guid.Empty)
            {
                throw new ArgumentException("Entity id is required.", nameof(entityId));
            }

            if (drafts == null || drafts.Count == 0)
            {
                return 0;
            }

            var savedCount = 0;
            foreach (var draft in drafts
                .Where(x => x != null)
                .GroupBy(x => x.ChunkId != Guid.Empty ? x.ChunkId.ToString("D") : $"sort:{x.SortOrder}")
                .Select(group => group.Last())
                .OrderBy(x => x.SortOrder))
            {
                var chunkDto = await FindChunkDtoAsync(entityId, draft, ct);
                if (chunkDto == null)
                {
                    continue;
                }

                var converted = await _htmlToRichTextBlocksConverter.ConvertHtmlAsync(draft.Html ?? string.Empty, ct);
                var blocks = converted.Blocks ?? new List<RichTextBlock>();
                var dataJson = RichTextChunkStorageSerializer.SerializeChunkData(blocks);

                chunkDto.Data = dataJson;
                chunkDto.PlainText = RichTextChunkStorageSerializer.BuildPlainText(blocks);
                chunkDto.HtmlCache = RichTextChunkStorageSerializer.BuildHtmlCache(entityId, chunkDto.Id, blocks);
                chunkDto.BlockCount = blocks.Count;
                chunkDto.CharCount = RichTextChunkStorageSerializer.BuildCharCount(blocks);
                chunkDto.DataSizeBytes = DataPayloadEnvelopeSerializer.GetJsonLength(dataJson);
                chunkDto.Version = chunkDto.Version <= 0 ? 1 : chunkDto.Version + 1;
                chunkDto.Checksum = RichTextChunkStorageSerializer.BuildChecksum(dataJson);
                chunkDto.LastModifiedDate = DateTime.UtcNow;

                await _businessEntityDataChunkRepository.UpdateAsync(chunkDto, ct);
                await RebuildTableOfContentsPropertyAsync(chunkDto, blocks, ct);
                savedCount++;
            }

            if (savedCount > 0)
            {
                var entity = await _businessEntityHelper.GetBusinessEntityById(entityId);
                if (entity != null)
                {
                    entity.LastModifiedDate = DateTime.UtcNow;
                    await _dataProviderConnector.UpdateAsync(entity, ct);
                }
            }

            return savedCount;
        }

        private async Task<BusinessEntityDataChunkDto?> FindChunkDtoAsync(
            Guid entityId,
            RichTextDocumentChunkEditDraft draft,
            CancellationToken ct)
        {
            if (draft.ChunkId != Guid.Empty)
            {
                var byId = await _businessEntityDataChunkRepository.GetByIdAsync(draft.ChunkId, ct);
                if (byId != null && byId.BusinessEntityId == entityId)
                {
                    return byId;
                }
            }

            var bySortOrder = await _businessEntityDataChunkRepository.GetAllAsync(
                x => x.BusinessEntityId == entityId && x.SortOrder == draft.SortOrder,
                take: 1,
                ct: ct);
            return bySortOrder.FirstOrDefault();
        }

        private async Task RebuildTableOfContentsPropertyAsync(
            BusinessEntityDataChunkDto chunkDto,
            IReadOnlyList<RichTextBlock> blocks,
            CancellationToken ct)
        {
            await DeletePropertiesAsync(
                _businessEntityDataChunkPropertyRepository,
                chunkDto.Id,
                (int)BusinessEntityDataChunkPropertyTypeEnum.RichDocTableOfContents,
                ct);

            var tableOfContentsProperty = BuildTableOfContentsProperty(chunkDto, blocks);
            if (tableOfContentsProperty != null)
            {
                await _businessEntityDataChunkPropertyRepository.AddAsync(tableOfContentsProperty, ct);
            }
        }

        // Логирует каждое фактическое чтение rich-text chunk DTO для наблюдения за virtual viewport.
        private async Task LogChunkWindowReadAsync(
            Guid entityId,
            long normalizedStart,
            int requestedTake,
            int totalCount,
            IReadOnlyList<BusinessEntityDataChunkDto> chunkDtos)
        {
            if (_webLogger == null || chunkDtos.Count == 0)
            {
                return;
            }

            foreach (var chunkDto in chunkDtos)
            {
                await _webLogger.Information(
                    "[rich-doc-chunk-read] " +
                    $"chunkId={chunkDto.Id:D} " +
                    $"sortOrder={chunkDto.SortOrder} " +
                    $"windowStart={normalizedStart} " +
                    $"requestedTake={requestedTake} " +
                    $"totalChunks={totalCount} " +
                    $"blockCount={chunkDto.BlockCount} " +
                    $"charCount={chunkDto.CharCount} " +
                    $"dataSizeBytes={chunkDto.DataSizeBytes} " +
                    $"htmlLength={chunkDto.HtmlCache?.Length ?? 0}");
            }
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
        /// Добавляет новый импортированный контент в конец существующего rich-text документа.
        /// </summary>
        public async Task AppendImportedContentAsync(
            BusinessEntity.Core.Classes.BusinessEntity entity,
            RichTextDocument manifest,
            IReadOnlyList<RichTextDocumentChunk> appendedChunks,
            IReadOnlyList<RichTextEmbeddedFile>? appendedFiles,
            CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            // Сначала читаем текущий снимок, чтобы append строился поверх реального сохраненного состояния.
            var snapshot = await GetRichTextDocumentSnapshotAsync(entity.Id, ct);
            var existingChunks = snapshot?.Chunks ?? Array.Empty<RichTextDocumentChunk>();

            // Для нового пустого документа стартовый технический chunk убираем, чтобы импорт не создавал пустую строку сверху.
            var normalizedExistingChunks = IsOnlyInitialEmptyChunk(existingChunks)
                ? Array.Empty<RichTextDocumentChunk>()
                : existingChunks;

            var mergedChunks = new List<RichTextDocumentChunk>();
            var nextSortOrder = 0L;

            // Сохраняем существующие chunks в исходном порядке, но нормализуем SortOrder перед полной replace-операцией.
            foreach (var existingChunk in normalizedExistingChunks.OrderBy(x => x.SortOrder))
            {
                mergedChunks.Add(CloneChunkForSave(existingChunk, entity.Id, nextSortOrder++));
            }

            // Новые chunks добавляем строго снизу, после последнего существующего.
            foreach (var appendedChunk in appendedChunks ?? Array.Empty<RichTextDocumentChunk>())
            {
                mergedChunks.Add(CloneChunkForSave(appendedChunk, entity.Id, nextSortOrder++));
            }

            // Если после merge ничего не осталось, сохраняем один пустой технический chunk, как и в create-path.
            if (mergedChunks.Count == 0)
            {
                mergedChunks.Add(new RichTextDocumentChunk
                {
                    BusinessEntityId = entity.Id,
                    SortOrder = 0,
                    Blocks = new List<RichTextBlock>()
                });
            }

            // Embedded files не заменяем, а дозаписываем, чтобы прежние изображения документа сохранялись.
            await SaveRichTextDocumentAsync(
                entity,
                manifest,
                mergedChunks,
                appendedFiles,
                replaceExistingFiles: false,
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

        /// <summary>
        /// Клонирует chunk в новый runtime-экземпляр перед replace-save, чтобы не тащить старые служебные ссылки.
        /// </summary>
        private static RichTextDocumentChunk CloneChunkForSave(RichTextDocumentChunk source, Guid businessEntityId, long sortOrder)
        {
            return new RichTextDocumentChunk
            {
                Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id,
                BusinessEntityId = businessEntityId,
                SortOrder = sortOrder,
                Blocks = source.Blocks?.Select(CloneBlock).ToList() ?? new List<RichTextBlock>(),
                Version = source.Version <= 0 ? 1 : source.Version
            };
        }

        /// <summary>
        /// Клонирует один rich-text блок, чтобы merged save работал с независимым набором данных.
        /// </summary>
        private static RichTextBlock CloneBlock(RichTextBlock source)
        {
            return new RichTextBlock
            {
                Kind = source.Kind,
                Level = source.Level,
                Html = source.Html,
                ImageId = source.ImageId,
                DisplayVariant = source.DisplayVariant,
                AltText = source.AltText
            };
        }

        private static async Task DeletePropertiesAsync<TProperty>(
            IAsyncRepository<TProperty> repository,
            Guid parentEntityId,
            int propertyType,
            CancellationToken cancellationToken)
            where TProperty : class, IPropertyDto
        {
            var properties = await repository.GetAllAsync(
                p => p.ParentEntityId == parentEntityId && p.PropertyType == propertyType,
                ct: cancellationToken);
            foreach (var property in properties)
            {
                await repository.DeleteAsync(property.Id, cancellationToken);
            }
        }

        // Собирает property-строку с оглавлением чанка, если в нём есть heading-блоки H1-H3.
        private static BusinessEntityDataChunkPropertyDto? BuildTableOfContentsProperty(
            BusinessEntityDataChunkDto chunkDto,
            IReadOnlyList<RichTextBlock>? blocks)
        {
            if (blocks == null || blocks.Count == 0)
            {
                return null;
            }

            var entries = new List<RichTextDocumentTableOfContentsEntry>();
            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                var block = blocks[blockIndex];
                if (!string.Equals(block.Kind, "heading", StringComparison.Ordinal))
                {
                    continue;
                }

                var level = block.Level;
                if (level < 1 || level > 3)
                {
                    continue;
                }

                var title = RichTextChunkStorageSerializer.BuildInlineText(block.Html);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = $"Heading {blockIndex + 1}";
                }

                entries.Add(new RichTextDocumentTableOfContentsEntry
                {
                    ChunkId = chunkDto.Id,
                    ChunkSortOrder = chunkDto.SortOrder,
                    BlockIndex = blockIndex,
                    Level = level,
                    Title = title,
                    Anchor = RichTextChunkStorageSerializer.BuildBlockAnchor(chunkDto.Id, blockIndex)
                });
            }

            if (entries.Count == 0)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            return new BusinessEntityDataChunkPropertyDto
            {
                Id = Guid.NewGuid(),
                CreatedDate = now,
                LastModifiedDate = now,
                ParentEntityId = chunkDto.Id,
                PropertyType = (int)BusinessEntityDataChunkPropertyTypeEnum.RichDocTableOfContents,
                Data = JsonSerializer.Serialize(
                    new RichTextTableOfContentsPayload
                    {
                        Entries = entries.Select(x => new RichTextTableOfContentsPayloadEntry
                        {
                            ChunkId = x.ChunkId,
                            ChunkSortOrder = x.ChunkSortOrder,
                            BlockIndex = x.BlockIndex,
                            Level = x.Level,
                            Title = x.Title,
                            Anchor = x.Anchor
                        }).ToList()
                    },
                    StorageJsonOptions.Default),
                Metadata = JsonSerializer.Serialize(
                    new RichTextTableOfContentsMetadata
                    {
                        EntryCount = entries.Count
                    },
                    StorageJsonOptions.Default)
            };
        }

        // Читает entries оглавления из persisted chunk-property JSON.
        private static IReadOnlyList<RichTextDocumentTableOfContentsEntry> ReadTableOfContentsEntries(BusinessEntityDataChunkPropertyDto property)
        {
            if (string.IsNullOrWhiteSpace(property.Data))
            {
                return Array.Empty<RichTextDocumentTableOfContentsEntry>();
            }

            var payload = JsonSerializer.Deserialize<RichTextTableOfContentsPayload>(property.Data, StorageJsonOptions.Default)
                ?? new RichTextTableOfContentsPayload();

            if (payload.SchemaVersion != 1 ||
                !string.Equals(payload.Kind, "RichDocChunkTableOfContents", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported rich-text table of contents payload for property '{property.Id}'.");
            }

            return payload.Entries
                .Select(x => new RichTextDocumentTableOfContentsEntry
                {
                    ChunkId = x.ChunkId,
                    ChunkSortOrder = x.ChunkSortOrder,
                    BlockIndex = x.BlockIndex,
                    Level = x.Level,
                    Title = x.Title ?? string.Empty,
                    Anchor = x.Anchor ?? string.Empty
                })
                .ToList();
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

        /// <summary>
        /// Проверяет, состоит ли документ только из одного пустого стартового chunk-а.
        /// </summary>
        private static bool IsOnlyInitialEmptyChunk(IReadOnlyList<RichTextDocumentChunk> chunks)
        {
            if (chunks == null || chunks.Count != 1)
            {
                return false;
            }

            var chunk = chunks[0];
            return chunk.Blocks == null || chunk.Blocks.Count == 0;
        }

        // Builds a hierarchical H1-H3 tree from persisted flat table-of-contents entries.
        private static IReadOnlyList<RichTextDocumentTableOfContentsEntry> BuildTableOfContentsTree(IReadOnlyList<RichTextDocumentTableOfContentsEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<RichTextDocumentTableOfContentsEntry>();
            }

            var roots = new List<RichTextDocumentTableOfContentsEntry>();
            var stack = new Stack<RichTextDocumentTableOfContentsEntry>();

            foreach (var entry in entries
                .OrderBy(x => x.ChunkSortOrder)
                .ThenBy(x => x.BlockIndex))
            {
                var node = CloneTableOfContentsEntry(entry);
                while (stack.Count > 0 && stack.Peek().Level >= node.Level)
                {
                    stack.Pop();
                }

                if (stack.Count == 0)
                {
                    roots.Add(node);
                }
                else
                {
                    stack.Peek().Children.Add(node);
                }

                stack.Push(node);
            }

            return roots;
        }

        // Copies a persisted entry into a tree node with its own children collection.
        private static RichTextDocumentTableOfContentsEntry CloneTableOfContentsEntry(RichTextDocumentTableOfContentsEntry source)
        {
            return new RichTextDocumentTableOfContentsEntry
            {
                ChunkId = source.ChunkId,
                ChunkSortOrder = source.ChunkSortOrder,
                BlockIndex = source.BlockIndex,
                Level = source.Level,
                Title = source.Title,
                Anchor = source.Anchor,
                Children = new List<RichTextDocumentTableOfContentsEntry>()
            };
        }

        private sealed class RichTextTableOfContentsPayload
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; } = 1;

            [JsonPropertyName("kind")]
            public string Kind { get; set; } = "RichDocChunkTableOfContents";

            [JsonPropertyName("entries")]
            public List<RichTextTableOfContentsPayloadEntry> Entries { get; set; } = new();
        }

        private sealed class RichTextTableOfContentsPayloadEntry
        {
            [JsonPropertyName("chunkId")]
            public Guid ChunkId { get; set; }

            [JsonPropertyName("chunkSortOrder")]
            public long ChunkSortOrder { get; set; }

            [JsonPropertyName("blockIndex")]
            public int BlockIndex { get; set; }

            [JsonPropertyName("level")]
            public int Level { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("anchor")]
            public string? Anchor { get; set; }
        }

        private sealed class RichTextTableOfContentsMetadata
        {
            [JsonPropertyName("schemaVersion")]
            public int SchemaVersion { get; set; } = 1;

            [JsonPropertyName("kind")]
            public string Kind { get; set; } = "RichDocChunkTableOfContentsMetadata";

            [JsonPropertyName("entryCount")]
            public int EntryCount { get; set; }
        }
    }
}
