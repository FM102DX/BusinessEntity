using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BusinessEntity.Core.BaseClasses.Relations;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.WebLogger.Services;
using Microsoft.AspNetCore.Hosting;

namespace BusinessEntity.Services.BackupRestore;

// Постоянно запущенный orchestrator backup-а пространств.
public sealed class SpaceBackupService : BackgroundService
{
    private const string MetadataFileName = "backup-metadata.json";
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultBackupInterval = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SpaceBackupService> _logger;
    private readonly IWebLoggerService? _webLogger;
    private readonly IReadOnlyList<IBusinessEntityBackupHandler> _handlers;
    private readonly IAsyncRepository<BusinessEntityDto> _businessEntityRepository;
    private readonly IAsyncRepository<BusinessEntityRelationDto> _businessEntityRelationRepository;
    private readonly IAsyncRepository<BusinessEntityPropertyDto> _businessEntityPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataDto> _businessEntityDataRepository;
    private readonly IAsyncRepository<BusinessEntityDataPropertyDto> _businessEntityDataPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkDto> _businessEntityDataChunkRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkPropertyDto> _businessEntityDataChunkPropertyRepository;
    private readonly SemaphoreSlim _backupGate = new(1, 1);
    private readonly Dictionary<Guid, DateTime> _nextScheduledBackupUtcBySpaceId = new();

    public sealed class SpaceBackupRunResult
    {
        public Guid SpaceId { get; init; }
        public string SpaceName { get; init; } = string.Empty;
        public int ChangedEntityCount { get; init; }
        public bool RelationsChanged { get; init; }
        public bool ManifestUpdated { get; init; }
        public DateTime FinishedUtc { get; init; }
        public TimeSpan NextInterval { get; init; }
    }

    public sealed class SpaceBackupClearResult
    {
        public Guid SpaceId { get; init; }
        public string BackupFolder { get; init; } = string.Empty;
        public string DisplayBackupFolder { get; init; } = string.Empty;
        public bool Deleted { get; init; }
    }

    public SpaceBackupService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SpaceBackupService> logger,
        IEnumerable<IBusinessEntityBackupHandler> handlers,
        IAsyncRepository<BusinessEntityDto> businessEntityRepository,
        IAsyncRepository<BusinessEntityRelationDto> businessEntityRelationRepository,
        IAsyncRepository<BusinessEntityPropertyDto> businessEntityPropertyRepository,
        IAsyncRepository<BusinessEntityDataDto> businessEntityDataRepository,
        IAsyncRepository<BusinessEntityDataPropertyDto> businessEntityDataPropertyRepository,
        IAsyncRepository<BusinessEntityDataChunkDto> businessEntityDataChunkRepository,
        IAsyncRepository<BusinessEntityDataChunkPropertyDto> businessEntityDataChunkPropertyRepository,
        IWebLoggerService? webLogger = null)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
        _webLogger = webLogger;
        _handlers = handlers.ToList();
        _businessEntityRepository = businessEntityRepository;
        _businessEntityRelationRepository = businessEntityRelationRepository;
        _businessEntityPropertyRepository = businessEntityPropertyRepository;
        _businessEntityDataRepository = businessEntityDataRepository;
        _businessEntityDataPropertyRepository = businessEntityDataPropertyRepository;
        _businessEntityDataChunkRepository = businessEntityDataChunkRepository;
        _businessEntityDataChunkPropertyRepository = businessEntityDataChunkPropertyRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await LogInformationAsync("[space-backup] service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (IsEnabled())
                {
                    await RunDueBackupCycleAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Space backup cycle failed.");
                await LogErrorAsync($"[space-backup] cycle failed: {ex}");
            }

            try
            {
                await Task.Delay(GetPollInterval(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public async Task<SpaceBackupRunResult> RunManualBackupAsync(Guid spaceId, CancellationToken ct = default)
    {
        return await RunSpaceBackupAsync(spaceId, manual: true, ct);
    }

    public async Task<SpaceBackupClearResult> ClearSpaceBackupAsync(
        Guid spaceId,
        GenericSpaceProperties settings,
        CancellationToken ct = default)
    {
        await _backupGate.WaitAsync(ct);
        try
        {
            var backupRoot = GetEffectiveSpaceBackupFolder(spaceId, settings);
            var fullBackupRoot = Path.GetFullPath(backupRoot);
            var storageRoot = Path.GetFullPath(ResolveStorageRoot());

            if (!IsPathInside(fullBackupRoot, storageRoot))
            {
                throw new InvalidOperationException(
                    $"Нельзя очистить backup вне storage-root приложения: '{ToHostDisplayPath(fullBackupRoot)}'.");
            }

            var deleted = false;
            if (Directory.Exists(fullBackupRoot))
            {
                Directory.Delete(fullBackupRoot, recursive: true);
                deleted = true;
            }

            lock (_nextScheduledBackupUtcBySpaceId)
            {
                _nextScheduledBackupUtcBySpaceId.Remove(spaceId);
            }

            await LogInformationAsync($"[space-backup] spaceId={spaceId} backup cleared path={fullBackupRoot} deleted={deleted}");
            return new SpaceBackupClearResult
            {
                SpaceId = spaceId,
                BackupFolder = fullBackupRoot,
                DisplayBackupFolder = ToHostDisplayPath(fullBackupRoot),
                Deleted = deleted
            };
        }
        finally
        {
            _backupGate.Release();
        }
    }

    public string GetEffectiveSpaceBackupFolder(Guid spaceId, GenericSpaceProperties settings)
    {
        var backupRoot = ResolveBackupRoot();
        var configuredFolder = settings.BackupFolder?.Trim();
        if (string.IsNullOrWhiteSpace(configuredFolder))
        {
            return Path.Combine(backupRoot, "spaces", $"Space--{spaceId:D}");
        }

        var hostMappedFolder = TryMapHostPathToContainerPath(configuredFolder);
        if (!string.IsNullOrWhiteSpace(hostMappedFolder))
        {
            return hostMappedFolder;
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredFolder)
                ? configuredFolder
                : Path.Combine(backupRoot, configuredFolder));
    }

    public string GetDisplaySpaceBackupFolder(Guid spaceId, GenericSpaceProperties settings)
    {
        return ToHostDisplayPath(GetEffectiveSpaceBackupFolder(spaceId, settings));
    }

    public async Task<IReadOnlyList<SpaceBackupRunResult>> RunBackupCycleAsync(CancellationToken ct = default)
    {
        var entities = await _businessEntityRepository.GetAllAsync(ct: ct);
        if (entities.Count == 0)
        {
            return Array.Empty<SpaceBackupRunResult>();
        }

        var entityProperties = await _businessEntityPropertyRepository.GetAllAsync(ct: ct);
        var spaces = entities
            .Where(IsSpace)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var results = new List<SpaceBackupRunResult>();

        foreach (var space in spaces)
        {
            ct.ThrowIfCancellationRequested();
            var settings = ReadSpaceProperties(space.Id, entityProperties);
            if (!settings.DoBackup)
            {
                continue;
            }

            results.Add(await RunSpaceBackupAsync(space.Id, manual: false, ct));
        }

        return results;
    }

    private async Task<IReadOnlyList<SpaceBackupRunResult>> RunDueBackupCycleAsync(CancellationToken ct)
    {
        var entities = await _businessEntityRepository.GetAllAsync(ct: ct);
        if (entities.Count == 0)
        {
            return Array.Empty<SpaceBackupRunResult>();
        }

        var entityProperties = await _businessEntityPropertyRepository.GetAllAsync(ct: ct);
        var spaces = entities
            .Where(IsSpace)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var existingSpaceIds = spaces.Select(x => x.Id).ToHashSet();
        var dueSpaceIds = new List<Guid>();
        var now = DateTime.UtcNow;

        lock (_nextScheduledBackupUtcBySpaceId)
        {
            foreach (var removedSpaceId in _nextScheduledBackupUtcBySpaceId.Keys.Where(x => !existingSpaceIds.Contains(x)).ToList())
            {
                _nextScheduledBackupUtcBySpaceId.Remove(removedSpaceId);
            }

            foreach (var space in spaces)
            {
                var settings = ReadSpaceProperties(space.Id, entityProperties);
                if (!settings.DoBackup)
                {
                    _nextScheduledBackupUtcBySpaceId.Remove(space.Id);
                    continue;
                }

                if (!_nextScheduledBackupUtcBySpaceId.TryGetValue(space.Id, out var nextRunUtc))
                {
                    nextRunUtc = now;
                    _nextScheduledBackupUtcBySpaceId[space.Id] = nextRunUtc;
                }

                if (now >= nextRunUtc)
                {
                    dueSpaceIds.Add(space.Id);
                }
            }
        }

        var results = new List<SpaceBackupRunResult>();
        foreach (var spaceId in dueSpaceIds)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(await RunSpaceBackupAsync(spaceId, manual: false, ct));
        }

        return results;
    }

    private async Task<SpaceBackupRunResult> RunSpaceBackupAsync(Guid spaceId, bool manual, CancellationToken ct)
    {
        await _backupGate.WaitAsync(ct);
        try
        {
            var result = await RunSpaceBackupCoreAsync(spaceId, manual, ct);
            SetNextScheduledBackup(result.SpaceId, result.FinishedUtc + result.NextInterval);
            return result;
        }
        finally
        {
            _backupGate.Release();
        }
    }

    private async Task<SpaceBackupRunResult> RunSpaceBackupCoreAsync(Guid spaceId, bool manual, CancellationToken ct)
    {
        var entities = await _businessEntityRepository.GetAllAsync(ct: ct);
        var relations = await _businessEntityRelationRepository.GetAllAsync(ct: ct);
        var entityProperties = await _businessEntityPropertyRepository.GetAllAsync(ct: ct);
        var childrenByParentId = relations
            .Where(x => x.RelationType == BusinessEntityRelationTypeEnum.Contains.ToString())
            .GroupBy(x => x.ObjectAId)
            .ToDictionary(x => x.Key, x => x.Select(r => r.ObjectBId).Distinct().ToList());

        var entitiesById = entities.ToDictionary(x => x.Id);
        if (!entitiesById.TryGetValue(spaceId, out var space) || !IsSpace(space))
        {
            throw new InvalidOperationException($"Пространство с id '{spaceId}' не найдено.");
        }

        var spaceSettings = ReadSpaceProperties(space.Id, entityProperties);
        if (!spaceSettings.DoBackup)
        {
            throw new InvalidOperationException($"Backup выключен для пространства '{space.Name}'.");
        }

        var backupRoot = ResolveSpaceBackupRoot(space, spaceSettings);
        Directory.CreateDirectory(backupRoot);

        var entityIds = CollectSpaceEntityIds(space.Id, childrenByParentId, entitiesById);
        var changedEntityCount = 0;
        foreach (var entityId in entityIds)
        {
            ct.ThrowIfCancellationRequested();

            if (!entitiesById.TryGetValue(entityId, out var entity))
            {
                continue;
            }

            if (await BackupEntityIfDirtyAsync(entity, backupRoot, ct))
            {
                changedEntityCount++;
            }
        }

        var relationsChanged = await BackupRelationsIfDirtyAsync(
            space,
            entityIds,
            entitiesById,
            relations,
            backupRoot,
            ct);

        var staleEntityFoldersRemoved = CleanupStalePublishedEntityFolders(backupRoot, entityIds, entitiesById);
        var manifestUpdated = changedEntityCount > 0
            || relationsChanged
            || staleEntityFoldersRemoved
            || !File.Exists(Path.Combine(backupRoot, "manifest.json"));
        if (manifestUpdated)
        {
            await WriteManifestAsync(space, entityIds, entitiesById, relations, backupRoot, ct);
        }

        var result = new SpaceBackupRunResult
        {
            SpaceId = space.Id,
            SpaceName = space.Name,
            ChangedEntityCount = changedEntityCount,
            RelationsChanged = relationsChanged,
            ManifestUpdated = manifestUpdated,
            FinishedUtc = DateTime.UtcNow,
            NextInterval = GetBackupInterval(spaceSettings)
        };

        if (manifestUpdated || manual)
        {
            await LogInformationAsync($"[space-backup] space={space.Name} id={space.Id} backed up dirty entities={changedEntityCount} relationsChanged={relationsChanged} staleEntityFoldersRemoved={staleEntityFoldersRemoved} manual={manual}");
        }

        return result;
    }

    private async Task<bool> BackupEntityIfDirtyAsync(BusinessEntityDto entity, string backupRoot, CancellationToken ct)
    {
        var entityProperties = await _businessEntityPropertyRepository.GetAllAsync(x => x.ParentEntityId == entity.Id, ct: ct);
        var dataItems = await _businessEntityDataRepository.GetAllAsync(x => x.BusinessEntityId == entity.Id, ct: ct);
        var dataIds = dataItems.Select(x => x.Id).Distinct().ToList();
        var dataProperties = dataIds.Count == 0
            ? Array.Empty<BusinessEntityDataPropertyDto>()
            : await _businessEntityDataPropertyRepository.GetAllAsync(x => dataIds.Contains(x.ParentEntityId), ct: ct);

        var latestChunk = await _businessEntityDataChunkRepository.GetPageAsync(
            x => x.BusinessEntityId == entity.Id,
            x => x.LastModifiedDate,
            descending: true,
            take: 1,
            ct: ct);

        var watermark = MaxUtc(
            entity.LastModifiedDate,
            entityProperties.Select(x => x.LastModifiedDate),
            dataItems.Select(x => x.LastModifiedDate),
            dataProperties.Select(x => x.LastModifiedDate),
            latestChunk.Select(x => x.LastModifiedDate));

        var entityFolderName = BuildEntityFolderName(entity);
        var entityFolder = Path.Combine(backupRoot, "entities", entityFolderName);
        var lastBackedUpUtc = await ReadEntityLastBackedUpUtcAsync(entityFolder, ct);
        if (lastBackedUpUtc.HasValue && lastBackedUpUtc.Value >= watermark)
        {
            return false;
        }

        var handler = _handlers.FirstOrDefault(x => x.CanHandle(entity));
        if (handler == null)
        {
            _logger.LogWarning("No backup handler found for entity {EntityId} type {EntityType}.", entity.Id, ResolveEntityType(entity));
            return false;
        }

        var tempFolder = Path.Combine(backupRoot, "entities", ".in-progress", Guid.NewGuid().ToString("D"), entityFolderName);
        if (Directory.Exists(tempFolder))
        {
            Directory.Delete(tempFolder, recursive: true);
        }

        await handler.WriteBackupAsync(
            new BusinessEntityBackupWriteContext
            {
                Entity = entity,
                EntityNamePathSegment = BuildEntityNamePathSegment(entity),
                EntityProperties = entityProperties,
                DataItems = dataItems,
                DataProperties = dataProperties,
                Chunks = Array.Empty<BusinessEntityDataChunkDto>(),
                ChunkProperties = Array.Empty<BusinessEntityDataChunkPropertyDto>(),
                ReadChunksPageAsync = (skip, take, token) => _businessEntityDataChunkRepository.GetPageAsync(
                    x => x.BusinessEntityId == entity.Id,
                    x => x.SortOrder,
                    descending: false,
                    skip: skip,
                    take: take,
                    ct: token),
                ReadChunkPropertiesAsync = (chunkIds, token) => chunkIds.Count == 0
                    ? Task.FromResult<IReadOnlyList<BusinessEntityDataChunkPropertyDto>>(Array.Empty<BusinessEntityDataChunkPropertyDto>())
                    : _businessEntityDataChunkPropertyRepository.GetAllAsync(x => chunkIds.Contains(x.ParentEntityId), ct: token),
                EntityFolderPath = tempFolder,
                StorageRootPath = ResolveStorageRoot(),
                EntityWatermarkUtc = watermark
            },
            ct);

        ReplaceDirectory(tempFolder, entityFolder, Path.Combine(backupRoot, "entities"));
        DeleteStaleEntityFolders(entity, entityFolder, Path.Combine(backupRoot, "entities"));
        CleanupEmptyInProgressParents(tempFolder, Path.Combine(backupRoot, "entities", ".in-progress"));
        return true;
    }

    private async Task<bool> BackupRelationsIfDirtyAsync(
        BusinessEntityDto space,
        IReadOnlySet<Guid> entityIds,
        IReadOnlyDictionary<Guid, BusinessEntityDto> entitiesById,
        IReadOnlyList<BusinessEntityRelationDto> allRelations,
        string backupRoot,
        CancellationToken ct)
    {
        var relations = allRelations
            .Where(x => entityIds.Contains(x.ObjectAId) && entityIds.Contains(x.ObjectBId))
            .OrderBy(x => x.RelationType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.ObjectAId)
            .ThenBy(x => x.ObjectBId)
            .ThenBy(x => x.Id)
            .ToList();

        var watermark = relations.Count == 0
            ? NormalizeUtc(space.LastModifiedDate)
            : MaxUtc(relations.Select(x => x.LastModifiedDate));
        var relationsFolder = Path.Combine(backupRoot, "relations");
        var metadataPath = Path.Combine(relationsFolder, MetadataFileName);
        var lastBackedUpUtc = await ReadLastBackedUpUtcAsync(metadataPath, ct);
        if (lastBackedUpUtc.HasValue && lastBackedUpUtc.Value >= watermark && File.Exists(Path.Combine(relationsFolder, "index.json")))
        {
            return false;
        }

        var tempFolder = Path.Combine(backupRoot, ".in-progress", $"relations-{Guid.NewGuid():D}");
        Directory.CreateDirectory(tempFolder);
        var byEntityFolder = Path.Combine(tempFolder, "by-entity");

        var relationIndex = new List<object>();
        foreach (var relation in relations)
        {
            var endpointIds = new[] { relation.ObjectAId, relation.ObjectBId }
                .Distinct()
                .Where(entitiesById.ContainsKey)
                .ToList();
            var files = new List<string>();

            foreach (var endpointId in endpointIds)
            {
                var endpointFolder = BuildEntityFolderName(entitiesById[endpointId]);
                var filePath = Path.Combine(byEntityFolder, endpointFolder, BuildRelationFileName(relation));
                await WriteJsonAsync(
                    filePath,
                    new
                    {
                        SchemaVersion = 1,
                        Kind = "BusinessEntityRelation",
                        EndpointEntityId = endpointId,
                        EndpointDirection = ResolveRelationDirection(relation, endpointId),
                        relation.Id,
                        relation.ObjectAId,
                        relation.ObjectBId,
                        relation.RelationType,
                        RelationParams = JsonOrString(relation.RelationParams),
                        relation.CreatedDate,
                        relation.LastModifiedDate
                    },
                    ct);

                files.Add(ToRelativePath(tempFolder, filePath));
            }

            relationIndex.Add(new
            {
                relation.Id,
                relation.ObjectAId,
                relation.ObjectBId,
                relation.RelationType,
                Files = files
            });
        }

        await WriteJsonAsync(
            Path.Combine(tempFolder, "index.json"),
            new
            {
                SchemaVersion = 1,
                Kind = "BusinessEntityRelationsIndex",
                SpaceId = space.Id,
                Layout = "by-entity-one-file-per-relation",
                Items = relationIndex
            },
            ct);

        await WriteJsonAsync(
            Path.Combine(tempFolder, "relation-properties-index.json"),
            new
            {
                SchemaVersion = 1,
                Kind = "BusinessEntityRelationPropertiesIndex",
                SpaceId = space.Id,
                Items = Array.Empty<object>()
            },
            ct);

        await WriteJsonAsync(
            Path.Combine(tempFolder, MetadataFileName),
            new
            {
                SchemaVersion = 1,
                Kind = "SpaceRelationsBackupMetadata",
                SpaceId = space.Id,
                LastBackedUpUtc = DateTime.UtcNow,
                RelationsWatermarkUtc = watermark
            },
            ct);

        ReplaceDirectory(tempFolder, relationsFolder, backupRoot);
        CleanupEmptyInProgressParents(tempFolder, Path.Combine(backupRoot, ".in-progress"));
        return true;
    }

    private async Task WriteManifestAsync(
        BusinessEntityDto space,
        IReadOnlySet<Guid> entityIds,
        IReadOnlyDictionary<Guid, BusinessEntityDto> entitiesById,
        IReadOnlyList<BusinessEntityRelationDto> allRelations,
        string backupRoot,
        CancellationToken ct)
    {
        var entityFolders = new List<object>();
        foreach (var entity in entityIds
                     .Where(entitiesById.ContainsKey)
                     .Select(x => entitiesById[x])
                     .OrderBy(x => ResolveEntityType(x).ToString(), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            var entityFolder = Path.Combine(backupRoot, "entities", BuildEntityFolderName(entity));
            entityFolders.Add(new
            {
                entity.Id,
                EntityType = ResolveEntityType(entity).ToString(),
                entity.Name,
                Folder = ToRelativePath(backupRoot, entityFolder),
                LastBackedUpUtc = await ReadEntityLastBackedUpUtcAsync(entityFolder, ct)
            });
        }

        var relations = allRelations
            .Where(x => entityIds.Contains(x.ObjectAId) && entityIds.Contains(x.ObjectBId))
            .ToList();

        await WriteJsonAsync(
            Path.Combine(backupRoot, "manifest.json"),
            new
            {
                SchemaVersion = 1,
                Kind = "SpaceBackupEntityFolderLayout",
                Layout = "entity-folder",
                SpaceId = space.Id,
                SpaceName = space.Name,
                CreatedUtc = DateTime.UtcNow,
                LastUpdatedUtc = DateTime.UtcNow,
                ApplicationVersion = typeof(SpaceBackupService).Assembly.GetName().Version?.ToString() ?? string.Empty,
                IsComplete = true,
                EntityFolderNamePattern = "{entityType}--{entityId}--{entityName}",
                Counts = new
                {
                    Entities = entityFolders.Count,
                    Relations = relations.Count
                },
                Entities = entityFolders
            },
            ct);
    }

    private static IReadOnlySet<Guid> CollectSpaceEntityIds(
        Guid spaceId,
        IReadOnlyDictionary<Guid, List<Guid>> childrenByParentId,
        IReadOnlyDictionary<Guid, BusinessEntityDto> entitiesById)
    {
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(spaceId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!result.Add(currentId))
            {
                continue;
            }

            if (!childrenByParentId.TryGetValue(currentId, out var childIds))
            {
                continue;
            }

            foreach (var childId in childIds)
            {
                if (entitiesById.ContainsKey(childId))
                {
                    queue.Enqueue(childId);
                }
            }
        }

        return result;
    }

    private GenericSpaceProperties ReadSpaceProperties(Guid spaceId, IReadOnlyList<BusinessEntityPropertyDto> allProperties)
    {
        var property = allProperties
            .Where(x => x.ParentEntityId == spaceId && x.PropertyType == (int)BusinessEntityPropertyTypeEnum.GenericSpaceProperties)
            .OrderByDescending(x => x.LastModifiedDate)
            .FirstOrDefault();

        if (property == null || string.IsNullOrWhiteSpace(property.Data))
        {
            return new GenericSpaceProperties();
        }

        try
        {
            return JsonSerializer.Deserialize<GenericSpaceProperties>(property.Data, JsonOptions)
                ?? new GenericSpaceProperties();
        }
        catch (JsonException)
        {
            return new GenericSpaceProperties();
        }
    }

    private string ResolveStorageRoot()
    {
        var contentRoot = _environment.ContentRootPath ?? AppContext.BaseDirectory;
        var configuredRoot = _configuration["Storage:RootPath"];
        var storageRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(contentRoot, "App_Data", "RichDocumentData")
            : configuredRoot.Trim();

        return Path.GetFullPath(
            Path.IsPathRooted(storageRoot)
                ? storageRoot
                : Path.Combine(contentRoot, storageRoot));
    }

    private string ResolveBackupRoot()
    {
        var configuredRoot = _configuration["SpaceBackup:RootPath"];
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(ResolveStorageRoot(), "backups")
            : configuredRoot.Trim();

        return Path.GetFullPath(
            Path.IsPathRooted(root)
                ? root
                : Path.Combine(ResolveStorageRoot(), root));
    }

    private string ToHostDisplayPath(string containerPath)
    {
        var configuredHostRoot = _configuration["Storage:HostRootPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredHostRoot))
        {
            return containerPath;
        }

        var storageRoot = Path.GetFullPath(ResolveStorageRoot());
        var fullContainerPath = Path.GetFullPath(containerPath);
        var normalizedStorageRoot = EnsureTrailingSeparator(storageRoot);
        if (!fullContainerPath.StartsWith(normalizedStorageRoot, StringComparison.OrdinalIgnoreCase))
        {
            return containerPath;
        }

        var relativePath = Path.GetRelativePath(storageRoot, fullContainerPath);
        return CombineConfiguredPath(configuredHostRoot, relativePath);
    }

    private string? TryMapHostPathToContainerPath(string configuredFolder)
    {
        var configuredHostRoot = _configuration["Storage:HostRootPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(configuredHostRoot))
        {
            return null;
        }

        var normalizedConfiguredFolder = NormalizeConfiguredExternalPath(configuredFolder);
        var normalizedHostRoot = NormalizeConfiguredExternalPath(configuredHostRoot);
        if (string.IsNullOrWhiteSpace(normalizedConfiguredFolder) || string.IsNullOrWhiteSpace(normalizedHostRoot))
        {
            return null;
        }

        if (string.Equals(normalizedConfiguredFolder, normalizedHostRoot, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(ResolveStorageRoot());
        }

        var hostRootPrefix = $"{normalizedHostRoot}/";
        if (!normalizedConfiguredFolder.StartsWith(hostRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var relativePath = normalizedConfiguredFolder[hostRootPrefix.Length..];
        return Path.GetFullPath(Path.Combine(
            ResolveStorageRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CombineConfiguredPath(string root, string relativePath)
    {
        var separator = root.Contains('\\') ? '\\' : '/';
        var normalizedRoot = root.TrimEnd('\\', '/');
        var normalizedRelativePath = relativePath
            .Replace(Path.DirectorySeparatorChar, separator)
            .Replace(Path.AltDirectorySeparatorChar, separator)
            .TrimStart('\\', '/');

        return string.IsNullOrWhiteSpace(normalizedRelativePath)
            ? normalizedRoot
            : $"{normalizedRoot}{separator}{normalizedRelativePath}";
    }

    private static string NormalizeConfiguredExternalPath(string path)
    {
        return path.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private string ResolveSpaceBackupRoot(BusinessEntityDto space, GenericSpaceProperties settings)
    {
        return GetEffectiveSpaceBackupFolder(space.Id, settings);
    }

    private TimeSpan GetPollInterval()
    {
        return int.TryParse(_configuration["SpaceBackup:PollIntervalSeconds"], out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : DefaultPollInterval;
    }

    private static TimeSpan GetBackupInterval(GenericSpaceProperties settings)
    {
        return settings.BackupIntervalMinutes > 0
            ? TimeSpan.FromMinutes(settings.BackupIntervalMinutes)
            : DefaultBackupInterval;
    }

    private void SetNextScheduledBackup(Guid spaceId, DateTime nextRunUtc)
    {
        lock (_nextScheduledBackupUtcBySpaceId)
        {
            _nextScheduledBackupUtcBySpaceId[spaceId] = NormalizeUtc(nextRunUtc);
        }
    }

    private bool IsEnabled()
    {
        return !bool.TryParse(_configuration["SpaceBackup:Enabled"], out var enabled) || enabled;
    }

    private static bool IsSpace(BusinessEntityDto entity)
    {
        return entity.EntityType == BusinessEntityTypeEnum.Space || entity.BusinessEntityType == BusinessEntityTypeEnum.Space;
    }

    private static BusinessEntityTypeEnum ResolveEntityType(BusinessEntityDto entity)
    {
        return entity.EntityType == BusinessEntityTypeEnum.Undefined
            ? entity.BusinessEntityType
            : entity.EntityType;
    }

    private static string BuildEntityFolderName(BusinessEntityDto entity)
    {
        return $"{BuildEntityFolderPrefix(entity)}--{BuildEntityNamePathSegment(entity)}";
    }

    private static string BuildEntityFolderPrefix(BusinessEntityDto entity)
    {
        return $"{ResolveEntityType(entity)}--{entity.Id:D}";
    }

    private static string BuildEntityNamePathSegment(BusinessEntityDto entity)
    {
        return SanitizePathSegment(string.IsNullOrWhiteSpace(entity.Name)
            ? "Unnamed"
            : entity.Name);
    }

    private static void DeleteStaleEntityFolders(BusinessEntityDto entity, string currentEntityFolder, string entitiesRoot)
    {
        if (!Directory.Exists(entitiesRoot))
        {
            return;
        }

        var currentFullPath = Path.GetFullPath(currentEntityFolder);
        var prefix = BuildEntityFolderPrefix(entity);
        foreach (var directory in Directory.EnumerateDirectories(entitiesRoot)
                     .Where(x =>
                     {
                         var directoryName = Path.GetFileName(x);
                         return string.Equals(directoryName, prefix, StringComparison.OrdinalIgnoreCase)
                             || directoryName.StartsWith($"{prefix}--", StringComparison.OrdinalIgnoreCase);
                     }))
        {
            var directoryFullPath = Path.GetFullPath(directory);
            if (string.Equals(directoryFullPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsPathInside(directoryFullPath, entitiesRoot))
            {
                Directory.Delete(directoryFullPath, recursive: true);
            }
        }
    }

    private static bool CleanupStalePublishedEntityFolders(
        string backupRoot,
        IReadOnlySet<Guid> entityIds,
        IReadOnlyDictionary<Guid, BusinessEntityDto> entitiesById)
    {
        var entitiesRoot = Path.Combine(backupRoot, "entities");
        if (!Directory.Exists(entitiesRoot))
        {
            return false;
        }

        var expectedFolders = entityIds
            .Where(entitiesById.ContainsKey)
            .Select(x => BuildEntityFolderName(entitiesById[x]))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = false;

        foreach (var directory in Directory.EnumerateDirectories(entitiesRoot).ToList())
        {
            var directoryName = Path.GetFileName(directory);
            if (string.Equals(directoryName, ".in-progress", StringComparison.OrdinalIgnoreCase)
                || expectedFolders.Contains(directoryName))
            {
                continue;
            }

            var directoryFullPath = Path.GetFullPath(directory);
            if (!IsPathInside(directoryFullPath, entitiesRoot))
            {
                continue;
            }

            Directory.Delete(directoryFullPath, recursive: true);
            removed = true;
        }

        return removed;
    }

    private static string BuildRelationFileName(BusinessEntityRelationDto relation)
    {
        var relationType = SanitizePathSegment(string.IsNullOrWhiteSpace(relation.RelationType)
            ? "Relation"
            : relation.RelationType);
        return $"relation--{relationType}--{relation.Id:D}.json";
    }

    private static string ResolveRelationDirection(BusinessEntityRelationDto relation, Guid endpointId)
    {
        if (relation.ObjectAId == endpointId && relation.ObjectBId == endpointId)
        {
            return "Self";
        }

        return relation.ObjectAId == endpointId ? "Outgoing" : "Incoming";
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
            .ToHashSet();
        var chars = value.Select(x => invalidChars.Contains(x) || char.IsControl(x) ? '_' : x).ToArray();
        var sanitized = new string(chars).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "Unnamed";
        }

        return sanitized.Length <= 120
            ? sanitized
            : sanitized[..120].Trim();
    }

    private static bool IsPathInside(string candidatePath, string rootPath)
    {
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        var fullRootPath = Path.GetFullPath(rootPath);
        return string.Equals(fullCandidatePath, fullRootPath, StringComparison.OrdinalIgnoreCase)
            || fullCandidatePath.StartsWith(EnsureTrailingSeparator(fullRootPath), StringComparison.OrdinalIgnoreCase);
    }

    private static object JsonOrString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static DateTime MaxUtc(params object[] sources)
    {
        var max = DateTime.MinValue;
        foreach (var source in sources)
        {
            if (source is DateTime date)
            {
                max = Max(max, date);
            }
            else if (source is IEnumerable<DateTime> dates)
            {
                foreach (var item in dates)
                {
                    max = Max(max, item);
                }
            }
        }

        return max == DateTime.MinValue ? DateTime.UtcNow : max;
    }

    private static DateTime Max(DateTime left, DateTime right)
    {
        var normalized = NormalizeUtc(right);
        return normalized > left ? normalized : left;
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }

    private static async Task<DateTime?> ReadEntityLastBackedUpUtcAsync(string entityFolder, CancellationToken ct)
    {
        return await ReadLastBackedUpUtcAsync(Path.Combine(entityFolder, MetadataFileName), ct);
    }

    private static async Task<DateTime?> ReadLastBackedUpUtcAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("lastBackedUpUtc", out var property) &&
                property.TryGetDateTime(out var value))
            {
                return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static void ReplaceDirectory(string source, string target, string allowedRoot)
    {
        var fullAllowedRoot = EnsureTrailingSeparator(Path.GetFullPath(allowedRoot));
        var fullTarget = Path.GetFullPath(target);
        if (!fullTarget.StartsWith(fullAllowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Backup target path is outside allowed root: {fullTarget}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullTarget)!);
        if (Directory.Exists(fullTarget))
        {
            Directory.Delete(fullTarget, recursive: true);
        }

        Directory.Move(source, fullTarget);
    }

    private static void CleanupEmptyInProgressParents(string tempFolder, string stopFolder)
    {
        var stop = Path.GetFullPath(stopFolder);
        var current = Directory.GetParent(Path.GetFullPath(tempFolder));
        while (current != null && current.FullName.StartsWith(stop, StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(current.FullName) || Directory.EnumerateFileSystemEntries(current.FullName).Any())
            {
                break;
            }

            var parent = current.Parent;
            Directory.Delete(current.FullName);
            current = parent;
        }
    }

    private static string EnsureTrailingSeparator(string value)
    {
        return value.EndsWith(Path.DirectorySeparatorChar)
            ? value
            : value + Path.DirectorySeparatorChar;
    }

    private static string ToRelativePath(string root, string path)
    {
        return Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static async Task WriteJsonAsync(string path, object value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private async Task LogInformationAsync(string message)
    {
        _logger.LogInformation("{Message}", message);
        if (_webLogger != null)
        {
            await _webLogger.Information(message);
        }
    }

    private async Task LogErrorAsync(string message)
    {
        _logger.LogError("{Message}", message);
        if (_webLogger != null)
        {
            await _webLogger.Error(message);
        }
    }
}
