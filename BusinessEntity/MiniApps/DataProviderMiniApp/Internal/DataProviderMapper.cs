using BusinessEntity.Core.Classes;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Dtos;

namespace BusinessEntity.MiniApps.DataProviderMiniApp.Internal;

internal static class DataProviderMapper
{
    // Собирает runtime BusinessEntity из DTO-записи хранилища.
    public static BusinessEntity.Core.Classes.BusinessEntity ToBusinessEntity(BusinessEntityDto dto)
    {
        return new BusinessEntity.Core.Classes.BusinessEntity
        {
            Id = dto.Id,
            CreatedDate = dto.CreatedDate,
            LastModifiedDate = dto.LastModifiedDate,
            Name = dto.Name,
            BusinessEntityType = dto.BusinessEntityType,
            EntityType = dto.EntityType
        };
    }

    // Преобразует runtime BusinessEntity в DTO для хранения.
    public static BusinessEntityDto ToDto(BusinessEntity.Core.Classes.BusinessEntity entity)
    {
        return new BusinessEntityDto
        {
            Id = entity.Id,
            CreatedDate = entity.CreatedDate,
            LastModifiedDate = entity.LastModifiedDate,
            Name = entity.Name,
            BusinessEntityType = entity.BusinessEntityType,
            EntityType = entity.EntityType
        };
    }

    // Собирает runtime BusinessEntityRelation из DTO связи.
    public static BusinessEntityRelation ToBusinessEntityRelation(BusinessEntityRelationDto dto)
    {
        return new BusinessEntityRelation
        {
            Id = dto.Id,
            CreatedDate = dto.CreatedDate,
            LastModifiedDate = dto.LastModifiedDate,
            ObjectAId = dto.ObjectAId,
            ObjectBId = dto.ObjectBId,
            RelationType = dto.RelationType,
            RelationParams = dto.RelationParams
        };
    }

    // Преобразует runtime BusinessEntityRelation в DTO для хранения.
    public static BusinessEntityRelationDto ToDto(BusinessEntityRelation relation)
    {
        return new BusinessEntityRelationDto
        {
            Id = relation.Id,
            CreatedDate = relation.CreatedDate,
            LastModifiedDate = relation.LastModifiedDate,
            ObjectAId = relation.ObjectAId,
            ObjectBId = relation.ObjectBId,
            RelationType = relation.RelationType,
            RelationParams = relation.RelationParams
        };
    }

    // Собирает runtime BusinessEntityData из DTO и уже прочитанной строки payload.
    public static BusinessEntityData ToBusinessEntityData(BusinessEntityDataDto dto, string data)
    {
        return new BusinessEntityData
        {
            Id = dto.Id,
            CreatedDate = dto.CreatedDate,
            LastModifiedDate = dto.LastModifiedDate,
            EntityId = dto.BusinessEntityId,
            Data = data
        };
    }
}
