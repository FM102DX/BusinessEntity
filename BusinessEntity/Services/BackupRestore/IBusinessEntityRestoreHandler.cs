using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.Services.BackupRestore;

public interface IBusinessEntityRestoreHandler
{
    bool CanHandle(BusinessEntityDto sourceEntity);

    Task RestoreAsync(BusinessEntityRestoreContext context, CancellationToken ct = default);
}
