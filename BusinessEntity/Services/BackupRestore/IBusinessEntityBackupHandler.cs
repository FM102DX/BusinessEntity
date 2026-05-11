using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.Services.BackupRestore;

// Handler одного типа business entity в backup-контуре.
public interface IBusinessEntityBackupHandler
{
    bool CanHandle(BusinessEntityDto entity);

    Task WriteBackupAsync(BusinessEntityBackupWriteContext context, CancellationToken ct = default);
}

