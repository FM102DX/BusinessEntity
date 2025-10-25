using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using BusinessEntity.Core.Services;
using ReactiveUI;
using BusinessEntity.WebLogger.Services;

namespace BusinessEntity.Components
{
    public partial class Document : ComponentBase
    {
        [Parameter] public global::BusinessEntity.Core.Classes.BusinessEntity? Entity { get; set; }
        [Parameter] public IReadOnlyList<global::BusinessEntity.Core.Classes.BusinessEntityData>? DataList { get; set; }

        [Inject] public BusinessEntityHelper Helper { get; set; } = default!;
        [Inject] public IMessageBus MessageBus { get; set; } = default!;
        [Inject] public IWebLoggerService? WebLogger { get; set; }

        private bool IsEditing;
        private bool IsSaving;
        private string EditTitle = string.Empty;
        private string EditBody = string.Empty;
        private List<global::BusinessEntity.Core.Classes.BusinessEntityData> LocalData = new();

        private string ViewText => LocalData != null && LocalData.Any() ? string.Join("\n\n", LocalData.Select(d => d.Data)) : string.Empty;

        protected override void OnParametersSet()
        {
            if (Entity != null)
            {
                EditTitle = Entity.Name;
            }
            LocalData = DataList?.ToList() ?? new List<global::BusinessEntity.Core.Classes.BusinessEntityData>();
            EditBody = ViewText;
        }

        private void EnterEditMode()
        {
            IsEditing = true;
        }

        private void CancelEdit()
        {
            if (Entity != null)
            {
                EditTitle = Entity.Name;
            }
            EditBody = ViewText;
            IsEditing = false;
        }

        private async Task SaveAsync()
        {
            if (Entity == null) return;
            IsSaving = true;
            try
            {
                WebLogger?.Information($"[Doc] SaveAsync started: entityId={Entity.Id}, prevTitle='{Entity.Name}', newTitle='{EditTitle}'");
                // Update entity title
                Entity.Name = (EditTitle ?? string.Empty).Trim();

                // Take first chunk or create new
                var data = (LocalData != null && LocalData.Count > 0)
                    ? new global::BusinessEntity.Core.Classes.BusinessEntityData
                    {
                        Id = LocalData[0].Id,
                        CreatedDate = LocalData[0].CreatedDate,
                        LastModifiedDate = DateTime.UtcNow,
                        EntityId = Entity.Id,
                        Data = EditBody ?? string.Empty
                    }
                    : new global::BusinessEntity.Core.Classes.BusinessEntityData
                    {
                        EntityId = Entity.Id,
                        Data = EditBody ?? string.Empty
                    };

                await Helper.SaveEntity(Entity, data);
                WebLogger?.Debug($"[Doc] SaveEntity OK: entityId={Entity.Id}, dataId={data.Id}");

                // Update current view
                EditBody = data.Data;
                if (LocalData.Count > 0)
                {
                    LocalData[0] = data;
                }
                else
                {
                    LocalData.Add(data);
                }

                // Publish update for other components (tree, breadcrumbs, etc.)
                if (MessageBus == null)
                {
                    WebLogger?.Warning("[Doc] MessageBus is null, cannot publish EntityUpdatedMessage");
                }
                else
                {
                    try
                    {
                        var busHash = MessageBus.GetHashCode();
                        WebLogger?.Information($"[Doc] Publishing EntityUpdatedMessage: busHash={busHash}, entityId={Entity.Id}, name='{Entity.Name}'");
                        MessageBus.SendMessage(new BusinessEntity.Services.EntityUpdatedMessage(Entity));
                        WebLogger?.Information("[Doc] Published EntityUpdatedMessage successfully");
                    }
                    catch (Exception ex)
                    {
                        WebLogger?.Error(ex);
                    }
                }
                IsEditing = false;
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
