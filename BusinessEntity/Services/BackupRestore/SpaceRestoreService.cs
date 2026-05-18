using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.WebLogger.Services;
using Microsoft.AspNetCore.Hosting;

namespace BusinessEntity.Services.BackupRestore;

public sealed class SpaceRestoreService
{
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
    private readonly ILogger<SpaceRestoreService> _logger;
    private readonly IWebLoggerService? _webLogger;
    private readonly IReadOnlyList<IBusinessEntityRestoreHandler> _handlers;
    private readonly IAsyncRepository<BusinessEntityDto> _businessEntityRepository;
    private readonly IAsyncRepository<BusinessEntityRelationDto> _businessEntityRelationRepository;
    private readonly IAsyncRepository<BusinessEntityPropertyDto> _businessEntityPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataDto> _businessEntityDataRepository;
    private readonly IAsyncRepository<BusinessEntityDataPropertyDto> _businessEntityDataPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkDto> _businessEntityDataChunkRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkPropertyDto> _businessEntityDataChunkPropertyRepository;
    private readonly SemaphoreSlim _restoreGate = new(1, 1);

    public SpaceRestoreService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SpaceRestoreService> logger,
        IEnumerable<IBusinessEntityRestoreHandler> handlers,
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

    public sealed class SpaceRestoreRequest
    {
        public string BackupRootPath { get; init; } = string.Empty;

        public string? TargetSpaceName { get; init; }

        public bool DisableBackupForRestoredSpace { get; init; } = true;

        public bool CleanupOnFailure { get; init; } = true;
    }

    public sealed class SpaceRestoreResult
    {
        public Guid RestoreSessionId { get; init; }

        public string SourceBackupRoot { get; init; } = string.Empty;

        public Guid SourceSpaceId { get; init; }

        public Guid TargetSpaceId { get; init; }

        public string TargetSpaceName { get; init; } = string.Empty;

        public int RestoredEntityCount { get; init; }

        public int RestoredRelationCount { get; init; }

        public string RestoreReportPath { get; init; } = string.Empty;

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public RestoreIdMap IdMap { get; init; } = default!;
    }

    public Task<SpaceRestoreResult> RestoreSpaceAsync(
        string backupRootPath,
        string? targetSpaceName = null,
        CancellationToken ct = default)
    {
        return RestoreSpaceAsync(
            new SpaceRestoreRequest
            {
                BackupRootPath = backupRootPath,
                TargetSpaceName = targetSpaceName
            },
            ct);
    }

    public async Task<SpaceRestoreResult> RestoreSpaceAsync(SpaceRestoreRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _restoreGate.WaitAsync(ct);
        var tracker = new BusinessEntityRestoreWriteTracker();
        var warnings = new List<string>();
        try
        {
            var backupRoot = ResolveRestoreInputPath(request.BackupRootPath);
            var manifest = await ReadManifestAsync(backupRoot, ct);
            ValidateBackupRoot(backupRoot, manifest);

            var restoreSessionId = Guid.NewGuid();
            var targetSpaceId = Guid.NewGuid();
            var targetSpaceName = ResolveTargetSpaceName(manifest.SpaceName, request.TargetSpaceName);
            var idMap = new RestoreIdMap
            {
                RestoreSessionId = restoreSessionId,
                SourceBackupRoot = backupRoot,
                SourceSpaceId = manifest.SpaceId,
                TargetSpaceId = targetSpaceId
            };

            var plans = await BuildEntityPlansAsync(backupRoot, manifest, targetSpaceId, targetSpaceName, idMap, warnings, ct);

            await CreateEntityShellsAsync(plans, tracker, ct);
            await RestoreEntityPayloadsAsync(
                backupRoot,
                plans,
                idMap,
                tracker,
                request.DisableBackupForRestoredSpace,
                warnings,
                ct);
            await EnsureRestoredSpacePropertiesAsync(targetSpaceId, request.DisableBackupForRestoredSpace, tracker, ct);

            var restoredRelationCount = await RestoreRelationsAsync(backupRoot, idMap, tracker, warnings, ct);
            var reportPath = await WriteRestoreReportAsync(
                restoreSessionId,
                backupRoot,
                manifest,
                targetSpaceId,
                targetSpaceName,
                idMap,
                plans.Count,
                restoredRelationCount,
                warnings,
                ct);

            await LogInformationAsync(
                $"[space-restore] restored sourceSpaceId={manifest.SpaceId} targetSpaceId={targetSpaceId} entities={plans.Count} relations={restoredRelationCount} source={backupRoot}");

            return new SpaceRestoreResult
            {
                RestoreSessionId = restoreSessionId,
                SourceBackupRoot = backupRoot,
                SourceSpaceId = manifest.SpaceId,
                TargetSpaceId = targetSpaceId,
                TargetSpaceName = targetSpaceName,
                RestoredEntityCount = plans.Count,
                RestoredRelationCount = restoredRelationCount,
                RestoreReportPath = reportPath,
                Warnings = warnings,
                IdMap = idMap
            };
        }
        catch
        {
            if (request.CleanupOnFailure)
            {
                await CleanupPartialRestoreAsync(tracker, ct);
            }

            throw;
        }
        finally
        {
            _restoreGate.Release();
        }
    }

    private async Task CreateEntityShellsAsync(
        IReadOnlyList<EntityRestorePlan> plans,
        BusinessEntityRestoreWriteTracker tracker,
        CancellationToken ct)
    {
        foreach (var plan in plans.OrderByDescending(x => x.IsSourceSpace))
        {
            ct.ThrowIfCancellationRequested();

            if (await _businessEntityRepository.ExistsAsync(plan.TargetEntity.Id, ct))
            {
                throw new InvalidOperationException($"Cannot restore entity because target id collision occurred: '{plan.TargetEntity.Id}'.");
            }

            await _businessEntityRepository.AddAsync(plan.TargetEntity, ct);
            tracker.EntityIds.Add(plan.TargetEntity.Id);
        }
    }

    private async Task RestoreEntityPayloadsAsync(
        string backupRoot,
        IReadOnlyList<EntityRestorePlan> plans,
        RestoreIdMap idMap,
        BusinessEntityRestoreWriteTracker tracker,
        bool disableBackupForRestoredSpace,
        List<string> warnings,
        CancellationToken ct)
    {
        var storageRoot = ResolveStorageRoot();
        foreach (var plan in plans)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(plan.EntityFolderPath) || !Directory.Exists(plan.EntityFolderPath))
            {
                warnings.Add($"Entity '{plan.SourceEntity.Id}' has no backup folder; only shell was restored.");
                continue;
            }

            var handler = _handlers.FirstOrDefault(x => x.CanHandle(plan.SourceEntity));
            if (handler == null)
            {
                throw new InvalidOperationException(
                    $"No restore handler found for entity '{plan.SourceEntity.Id}' type '{ResolveEntityType(plan.SourceEntity)}'.");
            }

            await handler.RestoreAsync(
                new BusinessEntityRestoreContext
                {
                    BackupRootPath = backupRoot,
                    EntityFolderPath = plan.EntityFolderPath,
                    StorageRootPath = storageRoot,
                    SourceEntity = plan.SourceEntity,
                    TargetEntity = plan.TargetEntity,
                    IdMap = idMap,
                    WriteTracker = tracker,
                    DisableBackupForRestoredSpace = disableBackupForRestoredSpace,
                    Warnings = warnings
                },
                ct);
        }
    }

    private async Task<int> RestoreRelationsAsync(
        string backupRoot,
        RestoreIdMap idMap,
        BusinessEntityRestoreWriteTracker tracker,
        List<string> warnings,
        CancellationToken ct)
    {
        var records = await ReadRelationBackupRecordsAsync(backupRoot, warnings, ct);
        var restoredCount = 0;

        foreach (var record in records.OrderBy(x => x.RelationType, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id))
        {
            ct.ThrowIfCancellationRequested();

            if (!idMap.Entities.TryGetValue(record.ObjectAId, out var targetObjectAId)
                || !idMap.Entities.TryGetValue(record.ObjectBId, out var targetObjectBId))
            {
                throw new InvalidOperationException(
                    $"Relation '{record.Id}' references entity outside restore map: '{record.ObjectAId}' -> '{record.ObjectBId}'.");
            }

            var relation = new BusinessEntityRelationDto
            {
                Id = idMap.GetOrCreateRelationId(record.Id),
                CreatedDate = record.CreatedDate,
                LastModifiedDate = record.LastModifiedDate,
                ObjectAId = targetObjectAId,
                ObjectBId = targetObjectBId,
                RelationType = record.RelationType,
                RelationParams = record.RelationParams
            };

            await _businessEntityRelationRepository.AddAsync(relation, ct);
            tracker.RelationIds.Add(relation.Id);
            restoredCount++;
        }

        return restoredCount;
    }

    private async Task EnsureRestoredSpacePropertiesAsync(
        Guid targetSpaceId,
        bool disableBackupForRestoredSpace,
        BusinessEntityRestoreWriteTracker tracker,
        CancellationToken ct)
    {
        var propertyType = (int)BusinessEntityPropertyTypeEnum.GenericSpaceProperties;
        var existingProperties = await _businessEntityPropertyRepository.GetAllAsync(
            x => x.ParentEntityId == targetSpaceId && x.PropertyType == propertyType,
            ct: ct);
        if (existingProperties.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var property = new BusinessEntityPropertyDto
        {
            Id = Guid.NewGuid(),
            CreatedDate = now,
            LastModifiedDate = now,
            ParentEntityId = targetSpaceId,
            PropertyType = propertyType,
            Data = JsonSerializer.Serialize(
                new GenericSpaceProperties
                {
                    DoBackup = !disableBackupForRestoredSpace,
                    BackupFolder = string.Empty,
                    BackupIntervalMinutes = 5
                },
                JsonOptions),
            Metadata = nameof(GenericSpaceProperties)
        };

        await _businessEntityPropertyRepository.AddAsync(property, ct);
        tracker.EntityPropertyIds.Add(property.Id);
    }

    private async Task<IReadOnlyList<RelationBackupRecord>> ReadRelationBackupRecordsAsync(
        string backupRoot,
        List<string> warnings,
        CancellationToken ct)
    {
        var relationsFolder = Path.Combine(backupRoot, "relations");
        var byEntityFolder = Path.Combine(relationsFolder, "by-entity");
        var records = new Dictionary<Guid, RelationBackupRecord>();

        if (Directory.Exists(byEntityFolder))
        {
            foreach (var relationFile in Directory.EnumerateFiles(byEntityFolder, "relation--*.json", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                using var document = await ReadJsonDocumentAsync(relationFile, ct);
                var record = ReadRelationRecord(document.RootElement);
                if (record.Id == Guid.Empty)
                {
                    warnings.Add($"Skipped relation file with empty id: {relationFile}");
                    continue;
                }

                records[record.Id] = record;
            }
        }

        if (records.Count > 0)
        {
            return records.Values.ToList();
        }

        var indexPath = Path.Combine(relationsFolder, "index.json");
        if (!File.Exists(indexPath))
        {
            warnings.Add("Relations index not found; restored space will have no relations.");
            return Array.Empty<RelationBackupRecord>();
        }

        using var indexDocument = await ReadJsonDocumentAsync(indexPath, ct);
        if (!TryGetProperty(indexDocument.RootElement, "items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<RelationBackupRecord>();
        }

        foreach (var item in items.EnumerateArray())
        {
            var record = ReadRelationRecord(item);
            if (record.Id != Guid.Empty)
            {
                records[record.Id] = record;
            }
        }

        return records.Values.ToList();
    }

    private async Task<IReadOnlyList<EntityRestorePlan>> BuildEntityPlansAsync(
        string backupRoot,
        SpaceBackupManifest manifest,
        Guid targetSpaceId,
        string targetSpaceName,
        RestoreIdMap idMap,
        List<string> warnings,
        CancellationToken ct)
    {
        var entityFolders = ResolveEntityFolders(backupRoot, manifest);
        var plans = new List<EntityRestorePlan>();

        foreach (var entityFolder in entityFolders)
        {
            ct.ThrowIfCancellationRequested();

            var entityJsonPath = Path.Combine(entityFolder, "entity.json");
            if (!File.Exists(entityJsonPath))
            {
                throw new InvalidOperationException($"Entity folder does not contain entity.json: '{entityFolder}'.");
            }

            var sourceEntity = await ReadEntityAsync(entityJsonPath, ct);
            if (sourceEntity.Id == Guid.Empty)
            {
                throw new InvalidOperationException($"Entity file has empty id: '{entityJsonPath}'.");
            }

            var isSourceSpace = sourceEntity.Id == manifest.SpaceId;
            var targetEntityId = isSourceSpace ? targetSpaceId : Guid.NewGuid();
            if (idMap.Entities.ContainsKey(sourceEntity.Id))
            {
                throw new InvalidOperationException($"Duplicate entity id in backup: '{sourceEntity.Id}'.");
            }

            idMap.Entities[sourceEntity.Id] = targetEntityId;

            plans.Add(new EntityRestorePlan(
                sourceEntity,
                BuildTargetEntity(sourceEntity, targetEntityId, isSourceSpace ? targetSpaceName : sourceEntity.Name, isSourceSpace),
                entityFolder,
                isSourceSpace));
        }

        if (!idMap.Entities.ContainsKey(manifest.SpaceId))
        {
            warnings.Add($"Backup manifest space '{manifest.SpaceId}' is not present in entities; synthetic target space shell was created.");
            var sourceSpace = new BusinessEntityDto
            {
                Id = manifest.SpaceId,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
                Name = manifest.SpaceName,
                BusinessEntityType = BusinessEntityTypeEnum.Space,
                EntityType = BusinessEntityTypeEnum.Space
            };

            idMap.Entities[sourceSpace.Id] = targetSpaceId;
            plans.Insert(
                0,
                new EntityRestorePlan(
                    sourceSpace,
                    BuildTargetEntity(sourceSpace, targetSpaceId, targetSpaceName, isSourceSpace: true),
                    string.Empty,
                    IsSourceSpace: true));
        }

        return plans;
    }

    private static IReadOnlyList<string> ResolveEntityFolders(string backupRoot, SpaceBackupManifest manifest)
    {
        if (manifest.EntityFolders.Count > 0)
        {
            return manifest.EntityFolders
                .Select(x => Path.Combine(backupRoot, x.Replace('/', Path.DirectorySeparatorChar)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var entitiesRoot = Path.Combine(backupRoot, "entities");
        return Directory.Exists(entitiesRoot)
            ? Directory.EnumerateDirectories(entitiesRoot)
                .Where(x => !string.Equals(Path.GetFileName(x), ".in-progress", StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : Array.Empty<string>();
    }

    private static BusinessEntityDto BuildTargetEntity(
        BusinessEntityDto source,
        Guid targetId,
        string name,
        bool isSourceSpace)
    {
        var entityType = ResolveEntityType(source);
        var now = DateTime.UtcNow;
        return new BusinessEntityDto
        {
            Id = targetId,
            CreatedDate = isSourceSpace ? now : source.CreatedDate,
            LastModifiedDate = isSourceSpace ? now : source.LastModifiedDate,
            IsPublic = source.IsPublic,
            Name = string.IsNullOrWhiteSpace(name) ? source.Name : name,
            BusinessEntityType = source.BusinessEntityType == BusinessEntityTypeEnum.Undefined
                ? entityType
                : source.BusinessEntityType,
            EntityType = source.EntityType == BusinessEntityTypeEnum.Undefined
                ? entityType
                : source.EntityType
        };
    }

    private static string ResolveTargetSpaceName(string sourceSpaceName, string? requestedName)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return requestedName.Trim();
        }

        var sourceName = string.IsNullOrWhiteSpace(sourceSpaceName)
            ? "RestoredSpace"
            : sourceSpaceName.Trim();
        return $"{sourceName} restore {DateTime.Now:yyyyMMdd-HHmmss}";
    }

    private async Task<SpaceBackupManifest> ReadManifestAsync(string backupRoot, CancellationToken ct)
    {
        var manifestPath = Path.Combine(backupRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Backup manifest.json was not found.", manifestPath);
        }

        using var document = await ReadJsonDocumentAsync(manifestPath, ct);
        var root = document.RootElement;
        var entityFolders = new List<string>();
        if (TryGetProperty(root, "entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
        {
            foreach (var entity in entities.EnumerateArray())
            {
                var folder = ReadString(entity, "folder");
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    entityFolders.Add(folder);
                }
            }
        }

        return new SpaceBackupManifest
        {
            SchemaVersion = ReadInt(root, "schemaVersion", 0),
            Kind = ReadString(root, "kind"),
            Layout = ReadString(root, "layout"),
            SpaceId = ReadGuid(root, "spaceId", Guid.Empty),
            SpaceName = ReadString(root, "spaceName"),
            IsComplete = ReadBool(root, "isComplete", fallback: true),
            EntityFolders = entityFolders
        };
    }

    private static void ValidateBackupRoot(string backupRoot, SpaceBackupManifest manifest)
    {
        if (!Directory.Exists(backupRoot))
        {
            throw new DirectoryNotFoundException($"Backup root was not found: '{backupRoot}'.");
        }

        if (manifest.SchemaVersion != 1)
        {
            throw new NotSupportedException($"Unsupported backup schemaVersion '{manifest.SchemaVersion}'.");
        }

        if (!string.Equals(manifest.Kind, "SpaceBackupEntityFolderLayout", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported backup kind '{manifest.Kind}'.");
        }

        if (!string.Equals(manifest.Layout, "entity-folder", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Unsupported backup layout '{manifest.Layout}'.");
        }

        if (manifest.SpaceId == Guid.Empty)
        {
            throw new InvalidOperationException("Backup manifest does not contain source space id.");
        }

        if (!manifest.IsComplete)
        {
            throw new InvalidOperationException("Backup manifest is not complete.");
        }

        var entitiesRoot = Path.Combine(backupRoot, "entities");
        if (!Directory.Exists(entitiesRoot))
        {
            throw new DirectoryNotFoundException($"Backup entities folder was not found: '{entitiesRoot}'.");
        }
    }

    private static async Task<BusinessEntityDto> ReadEntityAsync(string entityJsonPath, CancellationToken ct)
    {
        using var document = await ReadJsonDocumentAsync(entityJsonPath, ct);
        var root = document.RootElement;
        var entityType = ReadEntityType(root, "entityType", BusinessEntityTypeEnum.Undefined);
        var businessEntityType = ReadEntityType(root, "businessEntityType", entityType);

        return new BusinessEntityDto
        {
            Id = ReadGuid(root, "id", Guid.Empty),
            CreatedDate = ReadDateTime(root, "createdDate", DateTime.UtcNow),
            LastModifiedDate = ReadDateTime(root, "lastModifiedDate", DateTime.UtcNow),
            IsPublic = ReadBool(root, "isPublic", fallback: false),
            Name = ReadString(root, "name"),
            EntityType = entityType,
            BusinessEntityType = businessEntityType
        };
    }

    private async Task<string> WriteRestoreReportAsync(
        Guid restoreSessionId,
        string backupRoot,
        SpaceBackupManifest manifest,
        Guid targetSpaceId,
        string targetSpaceName,
        RestoreIdMap idMap,
        int restoredEntityCount,
        int restoredRelationCount,
        IReadOnlyList<string> warnings,
        CancellationToken ct)
    {
        var reportFolder = Path.Combine(ResolveStorageRoot(), "restore-reports");
        Directory.CreateDirectory(reportFolder);

        var reportPath = Path.Combine(reportFolder, $"restore-report--{restoreSessionId:D}.json");
        await WriteJsonAsync(
            reportPath,
            new
            {
                SchemaVersion = 1,
                Kind = "SpaceRestoreReport",
                RestoreSessionId = restoreSessionId,
                SourceBackupRoot = backupRoot,
                SourceSpaceId = manifest.SpaceId,
                SourceSpaceName = manifest.SpaceName,
                TargetSpaceId = targetSpaceId,
                TargetSpaceName = targetSpaceName,
                RestoredEntityCount = restoredEntityCount,
                RestoredRelationCount = restoredRelationCount,
                FinishedUtc = DateTime.UtcNow,
                Warnings = warnings,
                IdMap = new
                {
                    idMap.Entities,
                    idMap.DataItems,
                    idMap.Chunks,
                    idMap.Properties,
                    idMap.Relations
                }
            },
            ct);

        return reportPath;
    }

    private async Task CleanupPartialRestoreAsync(BusinessEntityRestoreWriteTracker tracker, CancellationToken ct)
    {
        try
        {
            foreach (var relationId in tracker.RelationIds)
            {
                await _businessEntityRelationRepository.DeleteAsync(relationId, ct);
            }

            foreach (var propertyId in tracker.ChunkPropertyIds)
            {
                await _businessEntityDataChunkPropertyRepository.DeleteAsync(propertyId, ct);
            }

            foreach (var chunkId in tracker.ChunkIds)
            {
                await _businessEntityDataChunkRepository.DeleteAsync(chunkId, ct);
            }

            foreach (var propertyId in tracker.DataPropertyIds)
            {
                await _businessEntityDataPropertyRepository.DeleteAsync(propertyId, ct);
            }

            foreach (var dataId in tracker.DataIds)
            {
                await _businessEntityDataRepository.DeleteAsync(dataId, ct);
            }

            foreach (var propertyId in tracker.EntityPropertyIds)
            {
                await _businessEntityPropertyRepository.DeleteAsync(propertyId, ct);
            }

            foreach (var entityId in tracker.EntityIds)
            {
                await _businessEntityRepository.DeleteAsync(entityId, ct);
            }

            var storageRoot = ResolveStorageRoot();
            foreach (var folder in tracker.StorageFolders)
            {
                var fullFolder = Path.GetFullPath(folder);
                if (Directory.Exists(fullFolder) && IsPathInside(fullFolder, storageRoot))
                {
                    Directory.Delete(fullFolder, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup partial space restore.");
            await LogWarningAsync($"[space-restore] partial cleanup failed: {ex.Message}");
        }
    }

    private string ResolveRestoreInputPath(string rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new ArgumentException("Backup root path cannot be empty.", nameof(rawPath));
        }

        var trimmed = rawPath.Trim();
        var hostMappedFolder = TryMapHostPathToContainerPath(trimmed);
        if (!string.IsNullOrWhiteSpace(hostMappedFolder))
        {
            return hostMappedFolder;
        }

        return Path.GetFullPath(
            Path.IsPathRooted(trimmed)
                ? trimmed
                : Path.Combine(ResolveStorageRoot(), trimmed));
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

    private static string NormalizeConfiguredExternalPath(string path)
    {
        return path.Trim().Replace('\\', '/').TrimEnd('/');
    }

    private static bool IsPathInside(string candidatePath, string rootPath)
    {
        var fullCandidatePath = Path.GetFullPath(candidatePath);
        var fullRootPath = Path.GetFullPath(rootPath);
        return string.Equals(fullCandidatePath, fullRootPath, StringComparison.OrdinalIgnoreCase)
            || fullCandidatePath.StartsWith(EnsureTrailingSeparator(fullRootPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static RelationBackupRecord ReadRelationRecord(JsonElement element)
    {
        return new RelationBackupRecord(
            ReadGuid(element, "id", Guid.Empty),
            ReadGuid(element, "objectAId", Guid.Empty),
            ReadGuid(element, "objectBId", Guid.Empty),
            ReadString(element, "relationType"),
            ReadStorageString(element, "relationParams"),
            ReadDateTime(element, "createdDate", DateTime.UtcNow),
            ReadDateTime(element, "lastModifiedDate", DateTime.UtcNow));
    }

    private static BusinessEntityTypeEnum ResolveEntityType(BusinessEntityDto entity)
    {
        return entity.EntityType == BusinessEntityTypeEnum.Undefined
            ? entity.BusinessEntityType
            : entity.EntityType;
    }

    private static async Task<JsonDocument> ReadJsonDocumentAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static async Task WriteJsonAsync(string path, object value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(path, json, ct);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ReadStorageString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static string ReadString(JsonElement element, string name)
    {
        return TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static Guid ReadGuid(JsonElement element, string name, Guid fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed)
            ? parsed
            : fallback;
    }

    private static int ReadInt(JsonElement element, string name, int fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var parsed) => parsed,
            JsonValueKind.String when int.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static bool ReadBool(JsonElement element, string name, bool fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static DateTime ReadDateTime(JsonElement element, string name, DateTime fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();
        }

        return fallback;
    }

    private static BusinessEntityTypeEnum ReadEntityType(
        JsonElement element,
        string name,
        BusinessEntityTypeEnum fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String
            && Enum.TryParse<BusinessEntityTypeEnum>(value.GetString(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var rawValue)
            && Enum.IsDefined(typeof(BusinessEntityTypeEnum), rawValue))
        {
            return (BusinessEntityTypeEnum)rawValue;
        }

        return fallback;
    }

    private async Task LogInformationAsync(string message)
    {
        _logger.LogInformation("{Message}", message);
        if (_webLogger != null)
        {
            await _webLogger.Information(message);
        }
    }

    private async Task LogWarningAsync(string message)
    {
        _logger.LogWarning("{Message}", message);
        if (_webLogger != null)
        {
            await _webLogger.Warning(message);
        }
    }

    private sealed record EntityRestorePlan(
        BusinessEntityDto SourceEntity,
        BusinessEntityDto TargetEntity,
        string EntityFolderPath,
        bool IsSourceSpace);

    private sealed class SpaceBackupManifest
    {
        public int SchemaVersion { get; init; }

        public string Kind { get; init; } = string.Empty;

        public string Layout { get; init; } = string.Empty;

        public Guid SpaceId { get; init; }

        public string SpaceName { get; init; } = string.Empty;

        public bool IsComplete { get; init; }

        public List<string> EntityFolders { get; init; } = new();
    }

    private sealed record RelationBackupRecord(
        Guid Id,
        Guid ObjectAId,
        Guid ObjectBId,
        string RelationType,
        string RelationParams,
        DateTime CreatedDate,
        DateTime LastModifiedDate);
}
