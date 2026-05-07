using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.RichText;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts;

/// <summary>
/// Реальный CRUD-контракт mini-app поверх DTO-хранилища.
/// Этот сервис можно внедрять напрямую в классы, которым нужен обход без фасада/инициализатора.
/// </summary>
public interface IDataProviderCrudService
{
    Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<BusinessEntity.Core.Classes.BusinessEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TData?> GetDataAsync<TData>(Guid id, CancellationToken cancellationToken = default)
        where TData : class, IBusinessEntityData;
    Task UpdateDataAsync<TData>(Guid id, TData data, CancellationToken cancellationToken = default)
        where TData : class, IBusinessEntityData;
    Task<string?> GetDataPayloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessEntityDataDto?> GetDataPayloadRecordAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateDataPayloadAsync(Guid id, string payloadJson, bool hasVersions = false, CancellationToken cancellationToken = default);
    Task<BusinessEntity.Core.Classes.BusinessEntity> AddAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default);
    Task UpdateAsync(BusinessEntity.Core.Classes.BusinessEntity entityData, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntityRelation>> GetAllRelationsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessEntityRelation>> GetRelationsAsync(Guid objectAId, Guid objectBId, CancellationToken cancellationToken = default);
    Task<BusinessEntityRelation?> GetRelationByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessEntityRelation> CreateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default);
    Task UpdateRelationAsync(BusinessEntityRelation relation, CancellationToken cancellationToken = default);
    Task DeleteRelationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RichTextDocumentChunk>> GetRichTextChunksAsync(Guid businessEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RichTextDocumentTableOfContentsEntry>> GetRichTextTableOfContentsEntriesAsync(Guid businessEntityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RichTextDocumentTableOfContentsEntry>> RebuildRichTextTableOfContentsEntriesAsync(Guid businessEntityId, CancellationToken cancellationToken = default);
    Task ReplaceRichTextChunksAsync(Guid businessEntityId, IReadOnlyList<RichTextDocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task SaveRichTextEmbeddedFilesAsync(Guid businessEntityId, IReadOnlyList<RichTextEmbeddedFile> files, bool replaceExistingFiles, CancellationToken cancellationToken = default);
    Task<RichTextEmbeddedFileContent?> GetRichTextEmbeddedFileAsync(Guid businessEntityId, string imageId, string variant, CancellationToken cancellationToken = default);
    Task DeleteRichTextStorageAsync(Guid businessEntityId, CancellationToken cancellationToken = default);
}
