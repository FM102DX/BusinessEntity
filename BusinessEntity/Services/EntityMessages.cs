using System;

namespace BusinessEntity.Services
{
    // Messages used with ReactiveUI's IMessageBus to propagate entityData lifecycle events
    public record EntityUpdatedMessage(BusinessEntity.Core.Classes.BusinessEntity EntityData);
    public record EntityCreatedMessage(BusinessEntity.Core.Classes.BusinessEntity EntityData);
    public record EntityDeletedMessage(Guid EntityId);
}
