using BusinessEntity.Core.Classes;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Messages;

public sealed record GetBusinessEntitiesRequest(Guid RequestId);
public sealed record GetBusinessEntitiesResponse(Guid RequestId, IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity> Records, string? ErrorMessage = null);

public sealed record GetBusinessEntityByIdRequest(Guid RequestId, Guid Id);
public sealed record GetBusinessEntityByIdResponse(Guid RequestId, BusinessEntity.Core.Classes.BusinessEntity? Record, string? ErrorMessage = null);

public sealed record GetBusinessEntityDataRequest(Guid RequestId, Guid BusinessEntityId);
public sealed record GetBusinessEntityDataResponse(Guid RequestId, string? Data, string? ErrorMessage = null);

public sealed record AddBusinessEntityRequest(Guid RequestId, BusinessEntity.Core.Classes.BusinessEntity Record);
public sealed record AddBusinessEntityResponse(Guid RequestId, BusinessEntity.Core.Classes.BusinessEntity? Record, string? ErrorMessage = null);

public sealed record UpdateBusinessEntityRequest(Guid RequestId, BusinessEntity.Core.Classes.BusinessEntity Record);
public sealed record UpdateBusinessEntityResponse(Guid RequestId, bool Success, string? ErrorMessage = null);

public sealed record DeleteBusinessEntityRequest(Guid RequestId, Guid Id);
public sealed record DeleteBusinessEntityResponse(Guid RequestId, bool Success, string? ErrorMessage = null);

public sealed record ClearDataProviderStorageRequest(Guid RequestId);
public sealed record ClearDataProviderStorageResponse(Guid RequestId, bool Success, string? ErrorMessage = null);

public sealed record UpdateBusinessEntityDataRequest(Guid RequestId, Guid BusinessEntityId, string Data);
public sealed record UpdateBusinessEntityDataResponse(Guid RequestId, bool Success, string? ErrorMessage = null);

public sealed record GetAllRelationsRequest(Guid RequestId);
public sealed record GetAllRelationsResponse(Guid RequestId, IReadOnlyList<BusinessEntityRelation> Records, string? ErrorMessage = null);

public sealed record GetRelationsRequest(Guid RequestId, Guid ObjectAId, Guid ObjectBId);
public sealed record GetRelationsResponse(Guid RequestId, IReadOnlyList<BusinessEntityRelation> Records, string? ErrorMessage = null);

public sealed record GetRelationByIdRequest(Guid RequestId, Guid Id);
public sealed record GetRelationByIdResponse(Guid RequestId, BusinessEntityRelation? Record, string? ErrorMessage = null);

public sealed record CreateRelationRequest(Guid RequestId, BusinessEntityRelation Record);
public sealed record CreateRelationResponse(Guid RequestId, BusinessEntityRelation? Record, string? ErrorMessage = null);

public sealed record UpdateRelationRequest(Guid RequestId, BusinessEntityRelation Record);
public sealed record UpdateRelationResponse(Guid RequestId, bool Success, string? ErrorMessage = null);

public sealed record DeleteRelationRequest(Guid RequestId, Guid Id);
public sealed record DeleteRelationResponse(Guid RequestId, bool Success, string? ErrorMessage = null);
