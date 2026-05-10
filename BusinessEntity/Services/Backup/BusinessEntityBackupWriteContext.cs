using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.Services.Backup;

public sealed class BusinessEntityBackupWriteContext
{
    public const int DefaultChunkPageSize = 250;

    public BusinessEntityDto Entity { get; set; } = default!;

    public string EntityNamePathSegment { get; set; } = string.Empty;

    public IReadOnlyList<BusinessEntityPropertyDto> EntityProperties { get; set; } = Array.Empty<BusinessEntityPropertyDto>();

    public IReadOnlyList<BusinessEntityDataDto> DataItems { get; set; } = Array.Empty<BusinessEntityDataDto>();

    public IReadOnlyList<BusinessEntityDataPropertyDto> DataProperties { get; set; } = Array.Empty<BusinessEntityDataPropertyDto>();

    public IReadOnlyList<BusinessEntityDataChunkDto> Chunks { get; set; } = Array.Empty<BusinessEntityDataChunkDto>();

    public IReadOnlyList<BusinessEntityDataChunkPropertyDto> ChunkProperties { get; set; } = Array.Empty<BusinessEntityDataChunkPropertyDto>();

    public int ChunkPageSize { get; set; } = DefaultChunkPageSize;

    public Func<int, int, CancellationToken, Task<IReadOnlyList<BusinessEntityDataChunkDto>>>? ReadChunksPageAsync { get; set; }

    public Func<IReadOnlyList<Guid>, CancellationToken, Task<IReadOnlyList<BusinessEntityDataChunkPropertyDto>>>? ReadChunkPropertiesAsync { get; set; }

    public string EntityFolderPath { get; set; } = string.Empty;

    public string StorageRootPath { get; set; } = string.Empty;

    public DateTime EntityWatermarkUtc { get; set; }
}
