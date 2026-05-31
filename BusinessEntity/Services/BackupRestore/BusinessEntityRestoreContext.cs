using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.Services.BackupRestore;

public sealed class BusinessEntityRestoreContext
{
    public string BackupRootPath { get; set; } = string.Empty;

    public string EntityFolderPath { get; set; } = string.Empty;

    public string StorageRootPath { get; set; } = string.Empty;

    public BusinessEntityDto SourceEntity { get; set; } = default!;

    public BusinessEntityDto TargetEntity { get; set; } = default!;

    public RestoreIdMap IdMap { get; set; } = default!;

    public BusinessEntityRestoreWriteTracker WriteTracker { get; set; } = default!;

    public bool DisableBackupForRestoredSpace { get; set; } = true;

    public List<string> Warnings { get; set; } = new();
}
