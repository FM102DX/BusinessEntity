using System;

namespace BusinessEntity.Services
{
    // Messages used with ReactiveUI's IMessageBus to propagate entity lifecycle events
    public record EntityUpdatedMessage(BusinessEntity.Core.Classes.BusinessEntity Entity);
    public record EntityCreatedMessage(BusinessEntity.Core.Classes.BusinessEntity Entity);
    public record EntityDeletedMessage(Guid EntityId);
}
