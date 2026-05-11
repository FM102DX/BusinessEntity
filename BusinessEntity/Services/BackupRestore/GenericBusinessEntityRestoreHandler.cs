using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;
using BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

namespace BusinessEntity.Services.BackupRestore;

public sealed class GenericBusinessEntityRestoreHandler : IBusinessEntityRestoreHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IAsyncRepository<BusinessEntityPropertyDto> _businessEntityPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataDto> _businessEntityDataRepository;
    private readonly IAsyncRepository<BusinessEntityDataPropertyDto> _businessEntityDataPropertyRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkDto> _businessEntityDataChunkRepository;
    private readonly IAsyncRepository<BusinessEntityDataChunkPropertyDto> _businessEntityDataChunkPropertyRepository;

    public GenericBusinessEntityRestoreHandler(
        IAsyncRepository<BusinessEntityPropertyDto> businessEntityPropertyRepository,
        IAsyncRepository<BusinessEntityDataDto> businessEntityDataRepository,
        IAsyncRepository<BusinessEntityDataPropertyDto> businessEntityDataPropertyRepository,
        IAsyncRepository<BusinessEntityDataChunkDto> businessEntityDataChunkRepository,
        IAsyncRepository<BusinessEntityDataChunkPropertyDto> businessEntityDataChunkPropertyRepository)
    {
        _businessEntityPropertyRepository = businessEntityPropertyRepository;
        _businessEntityDataRepository = businessEntityDataRepository;
        _businessEntityDataPropertyRepository = businessEntityDataPropertyRepository;
        _businessEntityDataChunkRepository = businessEntityDataChunkRepository;
        _businessEntityDataChunkPropertyRepository = businessEntityDataChunkPropertyRepository;
    }

    public bool CanHandle(BusinessEntityDto sourceEntity) => true;

    public async Task RestoreAsync(BusinessEntityRestoreContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.SourceEntity);
        ArgumentNullException.ThrowIfNull(context.TargetEntity);
        ArgumentNullException.ThrowIfNull(context.IdMap);

        await RestoreEntityPropertiesAsync(context, ct);
        await RestoreDataAsync(context, ct);
        CopyCanonicalFiles(context);
    }

    private async Task RestoreEntityPropertiesAsync(BusinessEntityRestoreContext context, CancellationToken ct)
    {
        var propertiesPath = Path.Combine(context.EntityFolderPath, "entity-properties.json");
        if (!File.Exists(propertiesPath))
        {
            return;
        }

        using var document = await ReadJsonDocumentAsync(propertiesPath, ct);
        if (!TryGetProperty(document.RootElement, "items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in items.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            var sourcePropertyId = ReadGuid(item, "id", Guid.NewGuid());
            var property = new BusinessEntityPropertyDto
            {
                Id = context.IdMap.GetOrCreatePropertyId(sourcePropertyId),
                CreatedDate = ReadDateTime(item, "createdDate", DateTime.UtcNow),
                LastModifiedDate = ReadDateTime(item, "lastModifiedDate", DateTime.UtcNow),
                ParentEntityId = context.TargetEntity.Id,
                PropertyType = ReadInt(item, "propertyType", 0),
                Data = ReadStorageString(item, "data"),
                Metadata = ReadString(item, "metadata")
            };

            if (IsTargetSpace(context)
                && property.PropertyType == (int)BusinessEntityPropertyTypeEnum.GenericSpaceProperties)
            {
                property.Data = BuildRestoredSpaceSettingsJson(property.Data, context.DisableBackupForRestoredSpace);
                property.Metadata = nameof(GenericSpaceProperties);
            }

            await _businessEntityPropertyRepository.AddAsync(property, ct);
            context.WriteTracker.EntityPropertyIds.Add(property.Id);
        }
    }

    private async Task RestoreDataAsync(BusinessEntityRestoreContext context, CancellationToken ct)
    {
        var dataFolder = Path.Combine(context.EntityFolderPath, "data");
        if (!Directory.Exists(dataFolder))
        {
            return;
        }

        foreach (var dataFile in Directory.EnumerateFiles(dataFolder, "business-entity-data--*.json").OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();

            using var document = await ReadJsonDocumentAsync(dataFile, ct);
            var root = document.RootElement;
            var sourceDataId = ReadGuid(root, "id", Guid.NewGuid());
            var data = new BusinessEntityDataDto
            {
                Id = context.IdMap.GetOrCreateDataId(sourceDataId),
                CreatedDate = ReadDateTime(root, "createdDate", DateTime.UtcNow),
                LastModifiedDate = ReadDateTime(root, "lastModifiedDate", DateTime.UtcNow),
                BusinessEntityId = context.TargetEntity.Id,
                Version = NormalizeVersion(ReadInt(root, "version", 1)),
                Data = ReadStorageString(root, "data")
            };

            await _businessEntityDataRepository.AddAsync(data, ct);
            context.WriteTracker.DataIds.Add(data.Id);
        }

        await RestoreDataPropertiesAsync(context, dataFolder, ct);
        await RestoreChunksAsync(context, dataFolder, ct);
    }

    private async Task RestoreDataPropertiesAsync(
        BusinessEntityRestoreContext context,
        string dataFolder,
        CancellationToken ct)
    {
        foreach (var propertyFile in Directory.EnumerateFiles(dataFolder, "data-properties--*.json").OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();

            using var document = await ReadJsonDocumentAsync(propertyFile, ct);
            var root = document.RootElement;
            var sourceParentDataId = ReadGuid(root, "parentDataId", Guid.Empty);
            if (!TryGetProperty(root, "items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var sourcePropertyId = ReadGuid(item, "id", Guid.NewGuid());
                var itemParentId = ReadGuid(item, "parentEntityId", sourceParentDataId);
                var parentDataId = context.IdMap.DataItems.TryGetValue(itemParentId, out var mappedParentId)
                    ? mappedParentId
                    : context.IdMap.DataItems.TryGetValue(sourceParentDataId, out mappedParentId)
                        ? mappedParentId
                        : Guid.Empty;

                if (parentDataId == Guid.Empty)
                {
                    context.Warnings.Add($"Skipped data property '{sourcePropertyId}' because parent data was not restored.");
                    continue;
                }

                var property = new BusinessEntityDataPropertyDto
                {
                    Id = context.IdMap.GetOrCreatePropertyId(sourcePropertyId),
                    CreatedDate = ReadDateTime(item, "createdDate", DateTime.UtcNow),
                    LastModifiedDate = ReadDateTime(item, "lastModifiedDate", DateTime.UtcNow),
                    ParentEntityId = parentDataId,
                    PropertyType = ReadInt(item, "propertyType", 0),
                    Data = ReadStorageString(item, "data"),
                    Metadata = ReadString(item, "metadata")
                };

                await _businessEntityDataPropertyRepository.AddAsync(property, ct);
                context.WriteTracker.DataPropertyIds.Add(property.Id);
            }
        }
    }

    private async Task RestoreChunksAsync(
        BusinessEntityRestoreContext context,
        string dataFolder,
        CancellationToken ct)
    {
        var chunksFolder = Path.Combine(dataFolder, "chunks");
        if (!Directory.Exists(chunksFolder))
        {
            return;
        }

        var isRichTextDocument = ResolveEntityType(context.TargetEntity) == BusinessEntityTypeEnum.RichTextDocument;
        foreach (var chunkFile in Directory.EnumerateFiles(chunksFolder, "chunk--*.json").OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();

            using var document = await ReadJsonDocumentAsync(chunkFile, ct);
            var root = document.RootElement;
            var sourceChunkId = ReadGuid(root, "id", Guid.NewGuid());
            var chunk = new BusinessEntityDataChunkDto
            {
                Id = context.IdMap.GetOrCreateChunkId(sourceChunkId),
                CreatedDate = ReadDateTime(root, "createdDate", DateTime.UtcNow),
                LastModifiedDate = ReadDateTime(root, "lastModifiedDate", DateTime.UtcNow),
                BusinessEntityId = context.TargetEntity.Id,
                SortOrder = ReadLong(root, "sortOrder", 0),
                Version = NormalizeVersion(ReadInt(root, "version", 1)),
                Data = ReadStorageString(root, "data"),
                PlainText = ReadNullableString(root, "plainText"),
                HtmlCache = ReadNullableString(root, "htmlCache"),
                BlockCount = ReadInt(root, "blockCount", 0),
                CharCount = ReadInt(root, "charCount", 0),
                DataSizeBytes = ReadInt(root, "dataSizeBytes", 0),
                Checksum = ReadNullableString(root, "checksum")
            };

            if (isRichTextDocument)
            {
                RebuildRichTextChunkDerivedFields(chunk, context.TargetEntity.Id);
            }

            await _businessEntityDataChunkRepository.AddAsync(chunk, ct);
            context.WriteTracker.ChunkIds.Add(chunk.Id);
        }

        await RestoreChunkPropertiesAsync(context, chunksFolder, isRichTextDocument, ct);
    }

    private async Task RestoreChunkPropertiesAsync(
        BusinessEntityRestoreContext context,
        string chunksFolder,
        bool isRichTextDocument,
        CancellationToken ct)
    {
        foreach (var propertyFile in Directory.EnumerateFiles(chunksFolder, "chunk-properties--*.json").OrderBy(x => x))
        {
            ct.ThrowIfCancellationRequested();

            using var document = await ReadJsonDocumentAsync(propertyFile, ct);
            var root = document.RootElement;
            var sourceParentChunkId = ReadGuid(root, "parentChunkId", Guid.Empty);
            if (!TryGetProperty(root, "items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in items.EnumerateArray())
            {
                var propertyType = ReadInt(item, "propertyType", 0);
                if (isRichTextDocument && propertyType == (int)BusinessEntityDataChunkPropertyTypeEnum.RichDocTableOfContents)
                {
                    continue;
                }

                var sourcePropertyId = ReadGuid(item, "id", Guid.NewGuid());
                var itemParentId = ReadGuid(item, "parentEntityId", sourceParentChunkId);
                var parentChunkId = context.IdMap.Chunks.TryGetValue(itemParentId, out var mappedParentId)
                    ? mappedParentId
                    : context.IdMap.Chunks.TryGetValue(sourceParentChunkId, out mappedParentId)
                        ? mappedParentId
                        : Guid.Empty;

                if (parentChunkId == Guid.Empty)
                {
                    context.Warnings.Add($"Skipped chunk property '{sourcePropertyId}' because parent chunk was not restored.");
                    continue;
                }

                var property = new BusinessEntityDataChunkPropertyDto
                {
                    Id = context.IdMap.GetOrCreatePropertyId(sourcePropertyId),
                    CreatedDate = ReadDateTime(item, "createdDate", DateTime.UtcNow),
                    LastModifiedDate = ReadDateTime(item, "lastModifiedDate", DateTime.UtcNow),
                    ParentEntityId = parentChunkId,
                    PropertyType = propertyType,
                    Data = ReadStorageString(item, "data"),
                    Metadata = ReadString(item, "metadata")
                };

                await _businessEntityDataChunkPropertyRepository.AddAsync(property, ct);
                context.WriteTracker.ChunkPropertyIds.Add(property.Id);
            }
        }
    }

    private static void CopyCanonicalFiles(BusinessEntityRestoreContext context)
    {
        var source = Path.Combine(context.EntityFolderPath, "files");
        if (!Directory.Exists(source))
        {
            return;
        }

        var target = Path.Combine(
            context.StorageRootPath,
            "business-entities",
            context.TargetEntity.Id.ToString("D"));

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        CopyDirectory(source, target);
        context.WriteTracker.StorageFolders.Add(target);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(filePath));
            File.Copy(filePath, targetPath, overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, Path.GetFileName(directoryPath));
            CopyDirectory(directoryPath, targetPath);
        }
    }

    private static void RebuildRichTextChunkDerivedFields(BusinessEntityDataChunkDto chunk, Guid targetBusinessEntityId)
    {
        try
        {
            var blocks = RichTextChunkStorageSerializer.DeserializeChunkData(chunk.Data);
            var dataJson = RichTextChunkStorageSerializer.SerializeChunkData(blocks);

            chunk.Data = dataJson;
            chunk.PlainText = RichTextChunkStorageSerializer.BuildPlainText(blocks);
            chunk.HtmlCache = RichTextChunkStorageSerializer.BuildHtmlCache(targetBusinessEntityId, chunk.Id, blocks);
            chunk.BlockCount = blocks.Count;
            chunk.CharCount = RichTextChunkStorageSerializer.BuildCharCount(blocks);
            chunk.DataSizeBytes = dataJson.Length;
            chunk.Checksum = RichTextChunkStorageSerializer.BuildChecksum(dataJson);
        }
        catch
        {
            // Keep canonical chunk payload intact if this is not a known rich-text chunk shape.
        }
    }

    private static string BuildRestoredSpaceSettingsJson(string sourceData, bool disableBackup)
    {
        GenericSpaceProperties settings;
        try
        {
            settings = string.IsNullOrWhiteSpace(sourceData)
                ? new GenericSpaceProperties()
                : JsonSerializer.Deserialize<GenericSpaceProperties>(sourceData, JsonOptions) ?? new GenericSpaceProperties();
        }
        catch (JsonException)
        {
            settings = new GenericSpaceProperties();
        }

        if (disableBackup)
        {
            settings.DoBackup = false;
        }

        settings.BackupFolder = string.Empty;
        settings.BackupIntervalMinutes = settings.BackupIntervalMinutes > 0 ? settings.BackupIntervalMinutes : 5;
        settings.Kind = nameof(GenericSpaceProperties);
        settings.SchemaVersion = settings.SchemaVersion > 0 ? settings.SchemaVersion : 1;

        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    private static bool IsTargetSpace(BusinessEntityRestoreContext context)
    {
        return context.TargetEntity.Id == context.IdMap.TargetSpaceId
            || ResolveEntityType(context.TargetEntity) == BusinessEntityTypeEnum.Space;
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

    private static string? ReadNullableString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : value.GetRawText();
    }

    private static Guid ReadGuid(JsonElement element, string name, Guid fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
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

    private static long ReadLong(JsonElement element, string name, long fallback)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var parsed) => parsed,
            JsonValueKind.String when long.TryParse(value.GetString(), out var parsed) => parsed,
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

    private static int NormalizeVersion(int version)
    {
        return version <= 0 ? 1 : version;
    }
}
